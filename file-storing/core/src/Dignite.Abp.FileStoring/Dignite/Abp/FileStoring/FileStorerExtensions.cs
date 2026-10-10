using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.FileStoring;

/// <summary>
/// Typed-container overloads, the same shape as ABP's <c>BlobContainerFactoryExtensions.Create&lt;TContainer&gt;</c>.
/// </summary>
public static class FileStorerExtensions
{
    public static Task<StoredFileInfo> StoreAsync<TContainer>(
        this IFileStorer fileStorer,
        [NotNull] string fileName,
        [NotNull] Stream stream,
        CancellationToken cancellationToken = default)
        where TContainer : class
    {
        return fileStorer.StoreAsync(
            BlobContainerNameAttribute.GetContainerName<TContainer>(),
            fileName,
            stream,
            cancellationToken);
    }

    public static Task<bool> DeleteAsync<TContainer>(
        this IFileStorer fileStorer,
        [NotNull] string blobName,
        CancellationToken cancellationToken = default)
        where TContainer : class
    {
        return fileStorer.DeleteAsync(
            BlobContainerNameAttribute.GetContainerName<TContainer>(),
            blobName,
            cancellationToken);
    }
}
