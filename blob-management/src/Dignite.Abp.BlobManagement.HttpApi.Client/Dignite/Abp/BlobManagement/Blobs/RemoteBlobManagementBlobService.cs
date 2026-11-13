using System;
using System.Threading.Tasks;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ExceptionHandling;
using Volo.Abp.Users;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// The remote <see cref="IBlobManagementBlobService"/>: the generated <see cref="BlobManagementBlobClientProxy"/>, keeping the contract the
/// in-process service keeps - a missing file is <c>null</c>, where the BLOB API answers 404 with
/// <see cref="BlobManagementErrorCodes.BlobNotFound"/>. The caller authenticates as an OAuth client: the host configures the
/// <c>Blob Management</c> remote service and its client credentials (ABP's <c>RemoteServices</c> and <c>IdentityClients</c>).
/// That token carries no user, so a save names the application's current user - the one the BLOB is counted for -
/// unless the caller named another, as the in-process service takes it from ABP's auditing.
/// </summary>
[DisableConventionalRegistration]
public class RemoteBlobManagementBlobService : IBlobManagementBlobService
{
    protected BlobManagementBlobClientProxy Proxy { get; }

    protected ICurrentUser CurrentUser { get; }

    public RemoteBlobManagementBlobService(BlobManagementBlobClientProxy proxy, ICurrentUser currentUser)
    {
        Proxy = proxy;
        CurrentUser = currentUser;
    }

    public virtual Task<BlobManagementBlobDto> SaveAsync(string containerName, string blobName, IRemoteStreamContent content, bool overrideExisting = false, Guid? creatorId = null)
    {
        return Proxy.SaveAsync(containerName, blobName, content, overrideExisting, creatorId ?? CurrentUser.Id);
    }

    public virtual async Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, string blobName)
    {
        try
        {
            return await Proxy.GetOrNullAsync(containerName, blobName);
        }
        catch (System.Exception exception) when (exception is IHasErrorCode { Code: BlobManagementErrorCodes.BlobNotFound })
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
}
