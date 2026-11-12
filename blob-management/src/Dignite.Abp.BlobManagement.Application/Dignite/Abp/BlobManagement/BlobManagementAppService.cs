using System;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Localization;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The base of the BlobManagement's application services, with what the user-facing ones (the interaction entry) share: the
/// caller's user id, the user drives and the mapping of a descriptor to its DTO.
/// </summary>
public abstract class BlobManagementAppService : ApplicationService
{
    protected BlobManagementAppService()
    {
        LocalizationResource = typeof(BlobManagementResource);
        ObjectMapperContext = typeof(BlobManagementApplicationModule);
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
            "The BlobManagement's file API serves users. An application stores files through the BLOB storing provider.");
    }

    /// <summary>The configuration of the user drive <paramref name="containerName"/>; any other container is not found.</summary>
    protected virtual BlobManagementContainerConfiguration GetUserBlobManagement(string containerName)
    {
        return ContainerConfigurationProvider.GetBlobManagementContainerConfiguration(containerName, BlobManagementContainerKind.UserBlobManagement);
    }

    protected virtual bool IsUserBlobManagement(string containerName)
    {
        var drive = ContainerConfigurationProvider.Get(containerName).GetBlobManagementContainerConfiguration();
        return drive.IsBlobManagementContainer && drive.Kind == BlobManagementContainerKind.UserBlobManagement;
    }

    protected virtual BlobManagementFileDto Map(FileDescriptor file)
    {
        return new BlobManagementFileDto
        {
            Id = file.Id,
            CreationTime = file.CreationTime,
            CreatorId = file.CreatorId,
            LastModificationTime = file.LastModificationTime,
            ContainerName = file.ContainerName,
            Name = file.Name,
            MimeType = file.MimeType,
            Size = file.Size,
            Url = GetUserBlobManagement(file.ContainerName).PublicRead
                ? BlobManagementPublicUrl.Build(file.ContainerName, file.BlobName, file.TenantId)
                : null
        };
    }
}
