namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// The <see cref="Volo.Abp.BusinessException"/> codes thrown by the contributors and
/// <see cref="IMimeTypeDetector"/>; localized by <c>BlobStoringPipelineResource</c>.
/// </summary>
public static class BlobStoringPipelineErrorCodes
{
    public const string Namespace = "Dignite.Abp.BlobStoring";

    /// <summary>
    /// The content is larger than the container allows. Data: <c>MaxSizeInBytes</c>.
    /// </summary>
    public const string ContentTooLarge = Namespace + ":ContentTooLarge";

    /// <summary>
    /// The content contradicts the extension of the BLOB name (an executable named <c>.png</c>).
    /// Data: <c>Extension</c>.
    /// </summary>
    public const string ContentTypeMismatch = Namespace + ":ContentTypeMismatch";

    /// <summary>
    /// The detected content type is not in the container's allowed list. Data: <c>ContentType</c>.
    /// </summary>
    public const string ContentTypeNotAllowed = Namespace + ":ContentTypeNotAllowed";
}
