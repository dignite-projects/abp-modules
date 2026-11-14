using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Authorization;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Abp.BlobStoring.Imaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BlobStoring;
using Volo.Abp.Features;

namespace Dignite.Abp.BlobManagement.Containers;

/// <summary>
/// The managed containers the Blob Management host declares, with what a file input needs to offer an upload - the
/// size cap, the allowed types, the image limits - read from the containers' own pipeline contributors rather than
/// restated, and what the caller may do with the directories of each. Part of the tenant's Blob Management: with
/// <see cref="BlobManagementFeatures.Enable"/> off it refuses every call.
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

    public virtual async Task<ListResultDto<BlobManagementContainerDto>> GetListAsync()
    {
        GetUserId();

        // ConfigureAll is how ABP enumerates the configured containers; nothing is changed here.
        var names = new List<string>();
        BlobStoringOptions.Containers.ConfigureAll((name, configuration) =>
        {
            if (configuration.GetBlobManagementContainerConfiguration().IsManaged)
            {
                names.Add(name);
            }
        });

        var context = await AccessService.BeginAsync();
        var containers = new List<BlobManagementContainerDto>();
        foreach (var name in names.OrderBy(name => name, StringComparer.Ordinal))
        {
            if (await AccessService.HasContainerAsync(context, name))
            {
                containers.Add(await MapAsync(context, name));
            }
        }

        return new ListResultDto<BlobManagementContainerDto>(containers);
    }

    public virtual async Task<BlobManagementContainerDto> GetAsync(string name)
    {
        GetUserId();
        GetContainer(name);
        return await MapAsync(await AccessService.BeginAsync(), name);
    }

    protected virtual async Task<BlobManagementContainerDto> MapAsync(BlobManagementAccessContext context, string name)
    {
        var container = GetContainer(name);
        var configuration = ContainerConfigurationProvider.Get(name);

        var resize = configuration.PipelineContributors.Contains<ImageResizeContributor>()
            ? configuration.GetImageResizeContributorConfiguration()
            : null;

        return new BlobManagementContainerDto
        {
            Name = name,
            AllowDirectories = container.AllowDirectories,
            MaxFileSize = BlobStreamBuffering.GetMaxBufferedBytes(configuration),
            AllowedFileExtensions = configuration.PipelineContributors.Contains<AllowedContentTypesContributor>()
                ? BlobManagementFileTypes.GetExtensions(configuration.GetAllowedContentTypesContributorConfiguration().AllowedContentTypes)
                : Array.Empty<string>(),
            ImageMaxWidth = resize is { MaxWidth: > 0 } ? resize.MaxWidth : null,
            ImageMaxHeight = resize is { MaxHeight: > 0 } ? resize.MaxHeight : null,
            Access = (await AccessService.GetAccessAsync(context, name)).ToDto()
        };
    }
}
