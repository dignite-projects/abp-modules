using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Dignite.Abp.FileStoring;

/// <summary>
/// Runs an upload through a blob container's <see cref="IFileHandler"/> pipeline and stores the result
/// in that container. This is the one place the pipeline is executed: callers (a media library, an
/// attachment feature, ...) own their own file metadata and authorization and call this to put bytes
/// into ABP BlobStoring safely.
/// <para>
/// Order: validate the container name → copy the upload into a buffer that is capped at the container's
/// size limit while copying → detect the MIME type from the content → run the container's handlers in
/// their configured order → hash the result (SHA-256) → generate the blob name → save it to the
/// container, deleting a partially written blob again if the save fails.
/// </para>
/// <para>
/// Content dedup and descriptor rows are not part of this service. A caller that persists metadata
/// writes it after <see cref="StoreAsync"/> returns and, if that write fails, calls
/// <see cref="DeleteAsync"/> with the returned blob name so the bytes do not outlive their row.
/// </para>
/// </summary>
public interface IFileStorer
{
    /// <summary>
    /// Stores <paramref name="stream"/> in the container named <paramref name="containerName"/>.
    /// </summary>
    /// <param name="containerName">A registered blob container name.</param>
    /// <param name="fileName">
    /// The client's file name. Only its extension is used: it is checked against the detected content
    /// (a mismatch is rejected) and refines generic containers such as ZIP into .docx/.xlsx. It is not
    /// stored.
    /// </param>
    /// <param name="stream">
    /// The upload. It is read once from its current position and never disposed; the caller keeps
    /// owning it.
    /// </param>
    /// <param name="cancellationToken">Flows to the copy, the handlers and the blob provider.</param>
    Task<StoredFileInfo> StoreAsync(
        [NotNull] string containerName,
        [NotNull] string fileName,
        [NotNull] Stream stream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the blob <paramref name="blobName"/> from the container named
    /// <paramref name="containerName"/>. Returns <c>false</c> when it did not exist.
    /// Whether the blob is still referenced by any metadata is the caller's decision.
    /// </summary>
    Task<bool> DeleteAsync(
        [NotNull] string containerName,
        [NotNull] string blobName,
        CancellationToken cancellationToken = default);
}
