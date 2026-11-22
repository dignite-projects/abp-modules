using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// The in-process <see cref="IBlobManagementBlobService"/>: the provider inside Blob Management's own host calls it, a
/// trusted caller, so it checks no permission and no client. Never exposed over HTTP
/// (<c>RemoteService(IsEnabled = false)</c>, so conventional controllers skip it); what a remote caller reaches is
/// <see cref="BlobManagementBlobIntegrationService"/>, which authorizes it first.
/// </summary>
[RemoteService(IsEnabled = false)]
public class BlobManagementBlobService : BlobManagementBlobServiceBase, IBlobManagementBlobService
{
    public BlobManagementBlobService(FileDescriptorManager fileDescriptorManager)
        : base(fileDescriptorManager)
    {
    }

    public virtual Task<BlobManagementBlobDto> SaveAsync(string containerName, string blobName, IRemoteStreamContent content, bool overrideExisting = false, Guid? creatorId = null, bool isPublic = false, string? sha256 = null)
    {
        return SaveBlobAsync(containerName, blobName, content, overrideExisting, creatorId, isPublic, sha256, clientId: null);
    }

    public virtual Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, string blobName)
    {
        return OpenBlobOrNullAsync(containerName, blobName);
    }

    public virtual Task<bool> ExistsAsync(string containerName, string blobName)
    {
        return BlobExistsAsync(containerName, blobName);
    }

    public virtual Task<bool> DeleteAsync(string containerName, string blobName)
    {
        return DeleteBlobAsync(containerName, blobName);
    }
}
