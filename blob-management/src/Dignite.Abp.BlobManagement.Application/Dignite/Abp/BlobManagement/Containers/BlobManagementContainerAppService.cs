using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Abp.BlobManagement.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BlobStoring;
using Volo.Abp.Features;

namespace Dignite.Abp.BlobManagement.Containers;

/// <summary>
/// The user drives the Blob Management host declares, with what a client needs to offer an upload - the size cap, the allowed
/// types, the image limits - read from the containers' own pipeline contributors rather than restated. Part of the
/// tenant's BlobManagement: with <see cref="BlobManagementFeatures.Enable"/> off it refuses every call.
/// </summary>
[Authorize]
[RequiresFeature(BlobManagementFeatures.Enable)]
public class BlobManagementContainerAppService : BlobManagementAppService, IBlobManagementContainerAppService
{
    protected AbpBlobStoringOptions BlobStoringOptions { get; }

    public BlobManagementContainerAppService(IOptions<AbpBlobStoringOptions> blobStoringOptions)
    {
        BlobStoringOptions = blobStoringOptions.Value;
    }

    /// <summary>Every user drive, whatever the caller may do in it: uploading is checked when it happens.</summary>
    public virtual async Task<ListResultDto<BlobManagementContainerDto>> GetListAsync()
    {
        GetUserId();

        // ConfigureAll is how ABP enumerates the configured containers; nothing is changed here.
        var names = new List<string>();
        BlobStoringOptions.Containers.ConfigureAll((name, configuration) =>
        {
            var drive = configuration.GetBlobManagementContainerConfiguration();
            if (drive.IsBlobManagementContainer && drive.Kind == BlobManagementContainerKind.UserBlobManagement)
            {
                names.Add(name);
            }
        });

        var containers = new List<BlobManagementContainerDto>();
        foreach (var name in names.OrderBy(name => name, StringComparer.Ordinal))
        {
            containers.Add(await GetAsync(name));
        }

        return new ListResultDto<BlobManagementContainerDto>(containers);
    }

    public virtual Task<BlobManagementContainerDto> GetAsync(string name)
    {
        GetUserId();
        var drive = GetUserBlobManagement(name);
        var configuration = ContainerConfigurationProvider.Get(name);

        var resize = configuration.PipelineContributors.Contains<ImageResizeContributor>()
            ? configuration.GetImageResizeContributorConfiguration()
            : null;

        return Task.FromResult(new BlobManagementContainerDto
        {
            Name = name,
            PublicRead = drive.PublicRead,
            MaxFileSize = BlobStreamBuffering.GetMaxBufferedBytes(configuration),
            AllowedFileExtensions = configuration.PipelineContributors.Contains<AllowedContentTypesContributor>()
                ? BlobManagementFileTypes.GetExtensions(configuration.GetAllowedContentTypesContributorConfiguration().AllowedContentTypes)
                : Array.Empty<string>(),
            ImageMaxWidth = resize is { MaxWidth: > 0 } ? resize.MaxWidth : null,
            ImageMaxHeight = resize is { MaxHeight: > 0 } ? resize.MaxHeight : null
        });
    }
}
