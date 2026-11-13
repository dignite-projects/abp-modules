using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Content;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// The in-process <see cref="IBlobManagementBlobService"/>: the provider inside Blob Management's own host calls it, a trusted caller,
/// so it checks no permission and no client - it goes straight to <see cref="FileDescriptorManager"/>. Never exposed
/// over HTTP (<c>RemoteService(IsEnabled = false)</c>, so conventional controllers skip it); the BLOB API authorizes its
/// remote callers before it reaches the storage.
/// </summary>
[RemoteService(IsEnabled = false)]
public class BlobManagementBlobService : BlobManagementAppService, IBlobManagementBlobService
{
    protected FileDescriptorManager FileDescriptorManager { get; }

    public BlobManagementBlobService(FileDescriptorManager fileDescriptorManager)
    {
        FileDescriptorManager = fileDescriptorManager;
    }

    public virtual Task<BlobManagementBlobDto> SaveAsync(string containerName, string blobName, IRemoteStreamContent content, bool overrideExisting = false, Guid? creatorId = null)
    {
        return SaveAsync(containerName, blobName, content, overrideExisting, creatorId, clientId: null);
    }

    public virtual async Task<IRemoteStreamContent?> GetOrNullAsync(string containerName, string blobName)
    {
        var file = await FileDescriptorManager.FindAsync(containerName, blobName);
        if (file == null)
        {
            return null;
        }

        var stream = await FileDescriptorManager.GetStreamOrNullAsync(file);
        return stream == null
            ? null
            : new RemoteStreamContent(stream, fileName: file.Name, contentType: file.MimeType, readOnlyLength: file.Size);
    }

    public virtual async Task<bool> ExistsAsync(string containerName, string blobName)
    {
        return await FileDescriptorManager.FindAsync(containerName, blobName) != null;
    }

    public virtual async Task<bool> DeleteAsync(string containerName, string blobName)
    {
        var file = await FileDescriptorManager.FindAsync(containerName, blobName);
        if (file == null)
        {
            return false;
        }

        await FileDescriptorManager.DeleteAsync(file);
        return true;
    }

    /// <summary>
    /// Stores the content, recording <paramref name="clientId"/> as its writer when a remote client sent it. The user
    /// it is counted for is <paramref name="creatorId"/>; without one, ABP's auditing records the current user.
    /// </summary>
    protected virtual async Task<BlobManagementBlobDto> SaveAsync(
        string containerName,
        string blobName,
        IRemoteStreamContent content,
        bool overrideExisting,
        Guid? creatorId,
        string? clientId)
    {
        Check.NotNull(content, nameof(content));
        CheckNameLength(blobName, nameof(blobName));

        // The stream is the caller's: the provider's pipeline stream, or the request body behind the BLOB API.
        var file = await FileDescriptorManager.CreateAsync(
            containerName, blobName, content.GetStream(), overrideExisting, clientId, creatorId);

        return new BlobManagementBlobDto
        {
            Name = file.Name,
            MimeType = file.MimeType,
            Size = file.Size,
            Hash = file.Hash
        };
    }
}
