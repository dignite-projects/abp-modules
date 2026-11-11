using System;
using Dignite.Drive.Files;
using Dignite.Drive.Localization;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;

namespace Dignite.Drive;

/// <summary>
/// The base of the Drive's application services, with what the user-facing ones (the interaction entry) share: the
/// caller's user id, the user drives and the mapping of a descriptor to its DTO.
/// </summary>
public abstract class DriveAppService : ApplicationService
{
    protected DriveAppService()
    {
        LocalizationResource = typeof(DriveResource);
        ObjectMapperContext = typeof(DriveApplicationModule);
    }

    protected IBlobContainerConfigurationProvider ContainerConfigurationProvider =>
        LazyServiceProvider.LazyGetRequiredService<IBlobContainerConfigurationProvider>();

    /// <summary>
    /// The caller's user id. The file API serves users only: a token without one - a client acting for itself - is
    /// refused, as the BLOB API refuses tokens with one.
    /// </summary>
    protected virtual Guid GetUserId()
    {
        return CurrentUser.Id ?? throw new AbpAuthorizationException(
            "The Drive's file API serves users. An application stores files through the BLOB storing provider.");
    }

    /// <summary>The configuration of the user drive <paramref name="containerName"/>; any other container is not found.</summary>
    protected virtual DriveContainerConfiguration GetUserDrive(string containerName)
    {
        return ContainerConfigurationProvider.GetDriveContainerConfiguration(containerName, DriveContainerKind.UserDrive);
    }

    protected virtual bool IsUserDrive(string containerName)
    {
        var drive = ContainerConfigurationProvider.Get(containerName).GetDriveContainerConfiguration();
        return drive.IsDriveContainer && drive.Kind == DriveContainerKind.UserDrive;
    }

    protected virtual DriveFileDto Map(FileDescriptor file)
    {
        return new DriveFileDto
        {
            Id = file.Id,
            CreationTime = file.CreationTime,
            CreatorId = file.CreatorId,
            LastModificationTime = file.LastModificationTime,
            ContainerName = file.ContainerName,
            Name = file.Name,
            MimeType = file.MimeType,
            Size = file.Size,
            Url = GetUserDrive(file.ContainerName).PublicRead
                ? DrivePublicUrl.Build(file.ContainerName, file.BlobName, file.TenantId)
                : null
        };
    }
}
