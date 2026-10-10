using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Dignite.Abp.FileStoring;

/// <summary>
/// Decides the MIME type of an upload from its content, never from what the uploader claims.
/// Replace the default <see cref="MimeTypeDetector"/> with ABP's
/// <c>[Dependency(ReplaceServices = true)]</c> to support more formats.
/// </summary>
public interface IMimeTypeDetector
{
    /// <summary>
    /// Returns the MIME type of the content of <paramref name="stream"/>, which must be seekable; its
    /// position is restored to 0 afterwards. <paramref name="fileName"/>'s extension is checked against
    /// the content: a disguised file (an executable named <c>.png</c>, text named <c>.pdf</c>) is
    /// rejected with <see cref="FileErrorCodes.Files.ContentTypeMismatch"/>.
    /// </summary>
    Task<string> DetectAsync(
        [NotNull] Stream stream,
        [NotNull] string fileName,
        CancellationToken cancellationToken = default);
}
