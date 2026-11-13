using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// What the BLOB storing provider (<c>Dignite.Abp.BlobStoring.BlobManagement</c>) calls: the bytes of an application's
/// container, kept in a managed container. The host decides which implementation answers: the in-process one when
/// it installs <c>Dignite.Abp.BlobManagement.Application</c> (a trusted caller), the HTTP client proxy when it installs
/// <c>Dignite.Abp.BlobManagement.HttpApi.Client</c> (a client authenticated by its credentials).
/// <para>
/// Every call works in the current tenant. A file is addressed by the caller's BLOB name.
/// </para>
/// </summary>
public interface IBlobManagementBlobService : IApplicationService
{
    /// <summary>
    /// Stores <paramref name="content"/> as <paramref name="blobName"/> in the Blob Management container
    /// <paramref name="containerName"/>. Fails with <see cref="BlobManagementErrorCodes.BlobAlreadyExists"/> when the name is
    /// taken and <paramref name="overrideExisting"/> is not set.
    /// </summary>
    /// <param name="creatorId">The user the BLOB is counted for in the usage figures; no right comes with it.
    /// <c>null</c>: the application's current user - in process as ABP's auditing records it, over HTTP as
    /// <c>Dignite.Abp.BlobManagement.HttpApi.Client</c> sends it, since the client's token carries no user.</param>
    Task<BlobManagementBlobDto> SaveAsync(string containerName, string blobName, IRemoteStreamContent content, bool overrideExisting = false, Guid? creatorId = null);

    /// <summary>The content of <paramref name="blobName"/>, or <c>null</c> when there is no such file.</summary>
    Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, string blobName);

    Task<bool> ExistsAsync(string containerName, string blobName);

    /// <summary>Deletes <paramref name="blobName"/>; <c>false</c> when there was no such file.</summary>
    Task<bool> DeleteAsync(string containerName, string blobName);
}
