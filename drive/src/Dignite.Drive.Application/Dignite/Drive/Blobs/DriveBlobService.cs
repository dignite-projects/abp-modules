using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Content;

namespace Dignite.Drive.Blobs;

/// <summary>
/// The in-process <see cref="IDriveBlobService"/>: the provider inside the Drive's own host calls it, a trusted caller,
/// so it checks no permission and no client - it goes straight to <see cref="FileDescriptorManager"/>. Never exposed
/// over HTTP (<c>RemoteService(IsEnabled = false)</c>, so conventional controllers skip it); the BLOB API authorizes its
/// remote callers before it reaches the storage.
/// </summary>
[RemoteService(IsEnabled = false)]
public class DriveBlobService : DriveAppService, IDriveBlobService
{
    protected FileDescriptorManager FileDescriptorManager { get; }

    public DriveBlobService(FileDescriptorManager fileDescriptorManager)
    {
        FileDescriptorManager = fileDescriptorManager;
    }

    public virtual Task<DriveBlobDto> SaveAsync(string containerName, string blobName, IRemoteStreamContent content, bool overrideExisting = false)
    {
        return SaveAsync(containerName, blobName, content, overrideExisting, clientId: null);
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

    /// <summary>Stores the content, recording <paramref name="clientId"/> as its writer when a remote client sent it.</summary>
    protected virtual async Task<DriveBlobDto> SaveAsync(
        string containerName,
        string blobName,
        IRemoteStreamContent content,
        bool overrideExisting,
        string? clientId)
    {
        Check.NotNull(content, nameof(content));

        // The stream is the caller's: the provider's pipeline stream, or the request body behind the BLOB API.
        var file = await FileDescriptorManager.CreateAsync(containerName, blobName, content.GetStream(), overrideExisting, clientId);

        return new DriveBlobDto
        {
            Name = file.Name,
            MimeType = file.MimeType,
            Size = file.Size,
            Hash = file.Hash
        };
    }
}
