using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobManagement.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BlobStoring;
using Volo.Abp.Features;
using Volo.Abp.Users;

namespace Dignite.Abp.BlobManagement.Usage;

/// <summary>
/// What the current tenant keeps in Blob Management, read from the descriptor table, for whoever holds the management
/// permission, in stored bytes: the sizes of the files. Part of the tenant's Blob Management: with
/// <see cref="BlobManagementFeatures.Enable"/> off it refuses every call.
/// </summary>
[Authorize(BlobManagementPermissions.Containers.Default)]
[RequiresFeature(BlobManagementFeatures.Enable)]
public class BlobManagementUsageAppService : BlobManagementAppService, IBlobManagementUsageAppService
{
    protected IFileDescriptorRepository FileRepository { get; }

    protected BlobManagementUsageManager UsageManager { get; }

    protected AbpBlobStoringOptions BlobStoringOptions { get; }

    public BlobManagementUsageAppService(
        IFileDescriptorRepository fileRepository,
        BlobManagementUsageManager usageManager,
        IOptions<AbpBlobStoringOptions> blobStoringOptions)
    {
        FileRepository = fileRepository;
        UsageManager = usageManager;
        BlobStoringOptions = blobStoringOptions.Value;
    }

    public virtual async Task<BlobManagementUsageDto> GetAsync()
    {
        GetUserId();
        var usages = (await FileRepository.GetContainerUsagesAsync()).ToDictionary(u => u.ContainerName, StringComparer.Ordinal);

        // Every managed container, empty ones too; then any other that still holds files, so the rows add up to the
        // tenant whatever the host has stopped managing. ConfigureAll is how ABP enumerates the configured containers;
        // nothing is changed here.
        var names = new SortedSet<string>(usages.Keys, StringComparer.Ordinal);
        BlobStoringOptions.Containers.ConfigureAll((name, configuration) =>
        {
            if (configuration.GetBlobManagementContainerConfiguration().IsManaged)
            {
                names.Add(name);
            }
        });

        var containers = names
            .Select(name => new BlobManagementContainerUsageDto
            {
                ContainerName = name,
                StoredBytes = usages.TryGetValue(name, out var usage) ? usage.StoredBytes : 0,
                FileCount = usage?.FileCount ?? 0
            })
            .ToList();

        return new BlobManagementUsageDto
        {
            StoredBytes = containers.Sum(c => c.StoredBytes),
            StorageSize = await UsageManager.GetStorageSizeAsync(),
            FileCount = containers.Sum(c => c.FileCount),
            Containers = containers
        };
    }

    public virtual async Task<PagedResultDto<BlobManagementUserUsageDto>> GetUsersAsync(GetBlobManagementUserUsagesInput input)
    {
        GetUserId();
        var containerName = NormalizeContainerName(input.ContainerName);

        var count = await FileRepository.GetCreatorCountAsync(containerName);
        var usages = await FileRepository.GetCreatorUsagesAsync(containerName, input.SkipCount, input.MaxResultCount);
        var userNames = await GetUserNamesAsync(usages.Where(u => u.CreatorId.HasValue).Select(u => u.CreatorId!.Value));

        return new PagedResultDto<BlobManagementUserUsageDto>(
            count,
            usages
                .Select(u => new BlobManagementUserUsageDto
                {
                    UserId = u.CreatorId,
                    UserName = u.CreatorId.HasValue ? userNames.GetValueOrDefault(u.CreatorId.Value) : null,
                    StoredBytes = u.StoredBytes,
                    FileCount = u.FileCount
                })
                .ToList());
    }

    public virtual async Task<ListResultDto<BlobManagementClientUsageDto>> GetClientsAsync(GetBlobManagementClientUsagesInput input)
    {
        GetUserId();
        var usages = await FileRepository.GetClientUsagesAsync(NormalizeContainerName(input.ContainerName));

        return new ListResultDto<BlobManagementClientUsageDto>(usages
            .Select(u => new BlobManagementClientUsageDto
            {
                ClientId = u.ClientId,
                StoredBytes = u.StoredBytes,
                FileCount = u.FileCount
            })
            .ToList());
    }

    protected virtual string? NormalizeContainerName(string? containerName)
    {
        return containerName.IsNullOrWhiteSpace() ? null : containerName!.Trim();
    }

    /// <summary>
    /// The names of the users of one page, where the host has ABP's user lookup (its Identity module, or that module's
    /// HTTP client) and still knows the user. Blob Management keeps no users of its own and does not require the
    /// lookup: without it, a user is shown by id.
    /// </summary>
    protected virtual async Task<Dictionary<Guid, string>> GetUserNamesAsync(IEnumerable<Guid> userIds)
    {
        var names = new Dictionary<Guid, string>();
        var lookup = LazyServiceProvider.LazyGetService<IExternalUserLookupServiceProvider>();
        if (lookup == null)
        {
            return names;
        }

        foreach (var userId in userIds)
        {
            var user = await lookup.FindByIdAsync(userId);
            if (user != null)
            {
                names[userId] = user.UserName;
            }
        }

        return names;
    }
}
