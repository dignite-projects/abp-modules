using System.Threading.Tasks;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ExceptionHandling;

namespace Dignite.Drive.Blobs;

/// <summary>
/// The remote <see cref="IDriveBlobService"/>: the generated <see cref="DriveBlobClientProxy"/>, keeping the contract the
/// in-process service keeps - a missing file is <c>null</c>, where the BLOB API answers 404 with
/// <see cref="DriveErrorCodes.BlobNotFound"/>. The caller authenticates as an OAuth client: the host configures the
/// <c>Drive</c> remote service and its client credentials (ABP's <c>RemoteServices</c> and <c>IdentityClients</c>).
/// </summary>
[DisableConventionalRegistration]
public class RemoteDriveBlobService : IDriveBlobService
{
    protected DriveBlobClientProxy Proxy { get; }

    public RemoteDriveBlobService(DriveBlobClientProxy proxy)
    {
        Proxy = proxy;
    }

    public virtual Task<DriveBlobDto> SaveAsync(string containerName, string blobName, IRemoteStreamContent content, bool overrideExisting = false)
    {
        return Proxy.SaveAsync(containerName, blobName, content, overrideExisting);
    }

    public virtual async Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, string blobName)
    {
        try
        {
            return await Proxy.GetOrNullAsync(containerName, blobName);
        }
        catch (System.Exception exception) when (exception is IHasErrorCode { Code: DriveErrorCodes.BlobNotFound })
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
