using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// Decides the MIME type of content from its bytes, never from what an uploader claims. Replace the default
/// <see cref="MimeTypeDetector"/> with ABP's <c>[Dependency(ReplaceServices = true)]</c> to change the policy,
/// or register another <c>FileSignatures.IFileFormatInspector</c> to recognise more formats.
/// </summary>
public interface IMimeTypeDetector
{
    /// <summary>
    /// Returns the MIME type of the content of <paramref name="stream"/>, which must be seekable; it is read
    /// from position 0 and its position is restored to 0 afterwards. When <paramref name="fileName"/> (a file
    /// or path-like BLOB name) has an extension, it is reconciled with the content: a disguised file (an
    /// executable named <c>.png</c>, text named <c>.pdf</c>) is rejected with
    /// <see cref="BlobStoringPipelineErrorCodes.ContentTypeMismatch"/>. Content that cannot be identified is
    /// <see cref="MimeTypeDetector.DefaultMimeType"/>.
    /// </summary>
    Task<string> DetectAsync(
        [NotNull] Stream stream,
        string? fileName = null,
        CancellationToken cancellationToken = default);
}
