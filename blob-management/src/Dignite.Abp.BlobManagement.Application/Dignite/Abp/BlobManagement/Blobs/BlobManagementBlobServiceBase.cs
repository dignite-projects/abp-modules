using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Threading;

namespace Dignite.Abp.BlobManagement.Blobs;

/// <summary>
/// What the two implementations of <see cref="IBlobManagementBlobService"/> share - the trusted in-process one and the
/// integration service behind an authorization: how a BLOB is saved, opened, found and deleted, on
/// <see cref="FileDescriptorManager"/>.
/// <para>
/// It has no public method on purpose. Each implementation writes every method of its contract itself, so one that is
/// added to the contract cannot be reached through the integration service before someone has written how its caller
/// is authorized.
/// </para>
/// </summary>
public abstract class BlobManagementBlobServiceBase : BlobManagementAppService
{
    protected FileDescriptorManager FileDescriptorManager { get; }

    /// <summary>
    /// ABP's ambient cancellation token, handed on to the domain: the request's, or the one the BLOB storing provider
    /// set from its caller's (<c>BlobManagementBlobProvider</c>) - an application service takes none as an argument.
    /// </summary>
    protected ICancellationTokenProvider CancellationTokenProvider =>
        LazyServiceProvider.LazyGetRequiredService<ICancellationTokenProvider>();

    protected BlobManagementBlobServiceBase(FileDescriptorManager fileDescriptorManager)
    {
        FileDescriptorManager = fileDescriptorManager;
    }

    /// <summary>
    /// Stores the content under <paramref name="blobName"/>, recording <paramref name="clientId"/> as its writer when a
    /// remote client sent it. The user it is counted for is <paramref name="creatorId"/>; without one, ABP's auditing
    /// records the current user.
    /// </summary>
    protected virtual async Task<BlobManagementBlobDto> SaveBlobAsync(
        string containerName,
        string blobName,
        IRemoteStreamContent content,
        bool overrideExisting,
        Guid? creatorId,
        bool isPublic,
        string? sha256,
        string? clientId)
    {
        Check.NotNull(content, nameof(content));
        CheckNameLength(blobName, nameof(blobName));

        // The stream is the caller's: the provider's pipeline stream, or the request body behind the BLOB API.
        var file = await FileDescriptorManager.SaveBlobAsync(
            containerName,
            blobName,
            content.GetStream(),
            new SaveBlobArgs
            {
                OverrideExisting = overrideExisting,
                IsPublic = isPublic,
                CreatorId = creatorId,
                ClientId = clientId,
                ExpectedSha256 = sha256
            },
            CancellationTokenProvider.Token);

        return new BlobManagementBlobDto
        {
            Name = file.Name,
            MimeType = file.MimeType,
            Size = file.Size,
            Hash = file.Hash
        };
    }

    /// <summary>The content saved under <paramref name="blobName"/>, or <c>null</c> when there is none.</summary>
    protected virtual async Task<IRemoteStreamContent?> OpenBlobOrNullAsync(string containerName, string blobName)
    {
        var cancellationToken = CancellationTokenProvider.Token;
        var file = await FileDescriptorManager.FindBlobAsync(containerName, blobName, cancellationToken);
        if (file == null)
        {
            return null;
        }

        var stream = await FileDescriptorManager.GetStreamOrNullAsync(file, cancellationToken);
        if (stream == null)
        {
            // Saved over between the lookup and the read: the BLOB that was found is deleted, the file is not gone.
            file = await FileDescriptorManager.FindReplacedBlobAsync(file, cancellationToken);
            if (file == null)
            {
                return null;
            }

            stream = await FileDescriptorManager.GetStreamOrNullAsync(file, cancellationToken);
        }

        return stream == null
            ? null
            : new RemoteStreamContent(stream, fileName: file.Name, contentType: file.MimeType, readOnlyLength: file.Size);
    }

    protected virtual async Task<bool> BlobExistsAsync(string containerName, string blobName)
    {
        return await FileDescriptorManager.FindBlobAsync(containerName, blobName, CancellationTokenProvider.Token) != null;
    }

    /// <summary>Deletes the BLOB saved under <paramref name="blobName"/>; <c>false</c> when there was none.</summary>
    protected virtual async Task<bool> DeleteBlobAsync(string containerName, string blobName)
    {
        var cancellationToken = CancellationTokenProvider.Token;
        var file = await FileDescriptorManager.FindBlobAsync(containerName, blobName, cancellationToken);
        if (file == null)
        {
            return false;
        }

        await FileDescriptorManager.DeleteAsync(file, cancellationToken);
        return true;
    }
}
