using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Authorization;
using Dignite.Abp.BlobManagement.Files;
using Dignite.Abp.BlobManagement.Public;
using Dignite.Abp.BlobManagement.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.BlobStoring;
using Volo.Abp.Validation;
using Volo.Abp.Validation.Localization;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The base of Blob Management's application services, with what they share: the caller's user id, the managed
/// containers, authorization of users through <see cref="BlobManagementAccessService"/> and the mapping of a descriptor
/// to its DTOs.
/// </summary>
public abstract class BlobManagementAppService : ApplicationService
{
    protected BlobManagementAppService()
    {
        LocalizationResource = typeof(BlobManagementResource);
    }

    protected IBlobContainerConfigurationProvider ContainerConfigurationProvider =>
        LazyServiceProvider.LazyGetRequiredService<IBlobContainerConfigurationProvider>();

    protected BlobManagementAccessService AccessService =>
        LazyServiceProvider.LazyGetRequiredService<BlobManagementAccessService>();

    /// <summary>
    /// The caller's user id, for the services that serve users - containers, directories, usage: a token without one -
    /// a client acting for itself - is refused there, as the BLOB API refuses tokens with one.
    /// </summary>
    protected virtual Guid GetUserId()
    {
        return CurrentUser.Id ?? throw new AbpAuthorizationException(
            "This part of Blob Management serves users. An application keeps files through the BLOB storing provider and the file service.");
    }

    /// <summary>The configuration of the managed container <paramref name="containerName"/>; any other is not found.</summary>
    protected virtual BlobManagementContainerConfiguration GetContainer(string containerName)
    {
        return ContainerConfigurationProvider.GetBlobManagementContainerConfiguration(containerName);
    }

    protected virtual bool IsManagedContainer(string containerName)
    {
        return ContainerConfigurationProvider.IsManagedContainer(containerName);
    }

    /// <summary>
    /// Refuses a file's name that is longer than a descriptor holds, as a validation error (HTTP 400) with ABP's own
    /// text for it. For a name that reaches the service outside a validated DTO - a BLOB name, an uploaded file's own
    /// name: the domain's argument check would otherwise answer it as an internal error.
    /// </summary>
    protected virtual void CheckNameLength(string? name, string memberName)
    {
        if (name == null || name.Length <= FileDescriptorConsts.MaxNameLength)
        {
            return;
        }

        var message = LazyServiceProvider.LazyGetRequiredService<IStringLocalizer<AbpValidationResource>>()[
            "The field {0} must be a string with a maximum length of {1}.", memberName, FileDescriptorConsts.MaxNameLength];
        throw new AbpValidationException(message, new[] { new ValidationResult(message, new[] { memberName }) });
    }

    protected virtual void Authorize(bool allowed)
    {
        if (!allowed)
        {
            throw new AbpAuthorizationException(code: AbpAuthorizationErrorCodes.GivenPolicyHasNotGranted);
        }
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
            DirectoryId = file.DirectoryId,
            Name = file.Name,
            IsAddressedByName = file.IsAddressedByName,
            MimeType = file.MimeType,
            Size = file.Size,
            ClientId = file.ClientId,
            IsPublic = file.IsPublic,
            IsPending = file.IsPending,
            // The public address of a file saved under a name is that name; a file kept by its id is addressed by its
            // physical BLOB, since its name may repeat - and renaming or moving it then leaves the address as it was.
            Url = file.IsPublic
                ? BlobManagementPublicUrl.Build(
                    file.ContainerName, file.IsAddressedByName ? file.Name : file.BlobName, file.TenantId)
                : null
        };
    }

    /// <summary>
    /// What a read of <paramref name="file"/> - public, or with a link - answers about it, under <paramref name="name"/>,
    /// the name the address answers with. The entity tag is the physical BLOB, which an overwrite changes, plus
    /// <paramref name="entityTagSuffix"/> for a rendition.
    /// </summary>
    protected virtual BlobManagementPublicFileDto MapReadFile(FileDescriptor file, string name, string entityTagSuffix = "")
    {
        return new BlobManagementPublicFileDto
        {
            Name = name,
            MimeType = file.MimeType,
            Size = file.Size,
            ContentId = file.BlobName,
            LastModified = file.CreationTime,
            EntityTag = file.BlobName + entityTagSuffix
        };
    }

    /// <summary>
    /// Weak comparison, as <c>If-None-Match</c> uses: <c>*</c> matches any file, and a tag matches when its opaque value
    /// is the same (the caller has already dropped a <c>W/</c> prefix).
    /// </summary>
    protected virtual bool IsNotModified(string entityTag, BlobManagementPublicReadRequest request)
    {
        return request.IfNoneMatchAny || request.IfNoneMatch.Any(tag => string.Equals(tag, entityTag, StringComparison.Ordinal));
    }
}
