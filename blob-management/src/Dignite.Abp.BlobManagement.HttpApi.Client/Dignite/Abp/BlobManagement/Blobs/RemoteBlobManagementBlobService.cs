using System;
using System.Threading.Tasks;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ExceptionHandling;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// The remote <see cref="IBlobManagementBlobService"/>: the generated <see cref="BlobManagementBlobIntegrationClientProxy"/>, keeping the contract the
/// in-process service keeps - a missing file is <c>null</c>, where the BLOB API answers 404 with
/// <see cref="BlobManagementErrorCodes.BlobNotFound"/>. The caller authenticates as an OAuth client: the host configures the
/// <c>Blob Management</c> remote service and its client credentials (ABP's <c>RemoteServices</c> and <c>IdentityClients</c>).
/// That token carries no user, so when the caller names no user a save names the one ABP's auditing would record in
/// process: the application's current user, when that user belongs to the current tenant, and none otherwise
/// (<see cref="GetCreatorId"/>).
/// </summary>
[DisableConventionalRegistration]
public class RemoteBlobManagementBlobService : IBlobManagementBlobService
{
    protected BlobManagementBlobIntegrationClientProxy Proxy { get; }

    protected ICurrentUser CurrentUser { get; }

    protected ICurrentTenant CurrentTenant { get; }

    public RemoteBlobManagementBlobService(
        BlobManagementBlobIntegrationClientProxy proxy,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant)
    {
        Proxy = proxy;
        CurrentUser = currentUser;
        CurrentTenant = currentTenant;
    }

    public virtual Task<BlobManagementBlobDto> SaveAsync(string containerName, string blobName, IRemoteStreamContent content, bool overrideExisting = false, Guid? creatorId = null, bool isPublic = false, string? sha256 = null)
    {
        return Proxy.SaveAsync(containerName, blobName, content, overrideExisting, creatorId ?? GetCreatorId(), isPublic, sha256);
    }

    public virtual async Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, string blobName)
    {
        try
        {
            return await Proxy.GetOrNullAsync(containerName, blobName);
        }
        catch (Exception exception) when (exception is IHasErrorCode { Code: BlobManagementErrorCodes.BlobNotFound })
        {
            return null;
        }
    }

    public virtual Task<bool> ExistsAsync(string containerName, string blobName)
    {
        return Proxy.ExistsAsync(containerName, blobName);
    }

    public virtual Task<bool> DeleteAsync(string containerName, string blobName)
    {
        return Proxy.DeleteAsync(containerName, blobName);
    }

    /// <summary>
    /// The user a file stored without a named one is counted for: the current user, when they belong to the current
    /// tenant - the rule of ABP's <c>AuditPropertySetter.SetCreatorId</c>, which the in-process service follows - and
    /// <c>null</c> otherwise, as for a host user working in a tenant.
    /// </summary>
    protected virtual Guid? GetCreatorId()
    {
        return CurrentUser.TenantId == CurrentTenant.Id ? CurrentUser.Id : null;
    }
}
