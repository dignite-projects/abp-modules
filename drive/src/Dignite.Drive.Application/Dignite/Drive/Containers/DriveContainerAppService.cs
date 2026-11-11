using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Drive.Features;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BlobStoring;
using Volo.Abp.Features;

namespace Dignite.Drive.Containers;

/// <summary>
/// The user drives the Drive host declares, with what a client needs to offer an upload - the size cap, the allowed
/// types, the image limits - read from the containers' own pipeline contributors rather than restated. Part of the
/// tenant's Drive: with <see cref="DriveFeatures.Enable"/> off it refuses every call.
/// </summary>
[Authorize]
[RequiresFeature(DriveFeatures.Enable)]
public class DriveContainerAppService : DriveAppService, IDriveContainerAppService
{
    protected AbpBlobStoringOptions BlobStoringOptions { get; }

    public DriveContainerAppService(IOptions<AbpBlobStoringOptions> blobStoringOptions)
    {
        BlobStoringOptions = blobStoringOptions.Value;
    }

    /// <summary>Every user drive, whatever the caller may do in it: uploading is checked when it happens.</summary>
    public virtual async Task<ListResultDto<DriveContainerDto>> GetListAsync()
    {
        GetUserId();

        // ConfigureAll is how ABP enumerates the configured containers; nothing is changed here.
        var names = new List<string>();
        BlobStoringOptions.Containers.ConfigureAll((name, configuration) =>
        {
            var drive = configuration.GetDriveContainerConfiguration();
            if (drive.IsDriveContainer && drive.Kind == DriveContainerKind.UserDrive)
            {
                names.Add(name);
            }
        });

        var containers = new List<DriveContainerDto>();
        foreach (var name in names.OrderBy(name => name, StringComparer.Ordinal))
        {
            containers.Add(await GetAsync(name));
        }

        return new ListResultDto<DriveContainerDto>(containers);
    }

    public virtual Task<DriveContainerDto> GetAsync(string name)
    {
        GetUserId();
        var drive = GetUserDrive(name);
        var configuration = ContainerConfigurationProvider.Get(name);

        var resize = configuration.PipelineContributors.Contains<ImageResizeContributor>()
            ? configuration.GetImageResizeContributorConfiguration()
            : null;

        return Task.FromResult(new DriveContainerDto
        {
            Name = name,
            PublicRead = drive.PublicRead,
            MaxFileSize = BlobStreamBuffering.GetMaxBufferedBytes(configuration),
            AllowedFileExtensions = configuration.PipelineContributors.Contains<AllowedContentTypesContributor>()
                ? DriveFileTypes.GetExtensions(configuration.GetAllowedContentTypesContributorConfiguration().AllowedContentTypes)
                : Array.Empty<string>(),
            ImageMaxWidth = resize is { MaxWidth: > 0 } ? resize.MaxWidth : null,
            ImageMaxHeight = resize is { MaxHeight: > 0 } ? resize.MaxHeight : null
        });
    }
}
