namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// The <see cref="Volo.Abp.BlobStoring.BlobContainerConfiguration"/> entries the contributors read.
/// Prefer the typed <c>*ContributorConfiguration</c> wrappers over setting these directly.
/// </summary>
public static class BlobStoringPipelineConfigurationNames
{
    private const string Prefix = "Dignite.Abp.BlobStoring.Pipeline.";

    /// <summary>
    /// <see cref="MaxSizeContributor"/>: the size cap in bytes (<see cref="long"/>).
    /// </summary>
    public const string MaxSizeInBytes = Prefix + "MaxSize.MaxSizeInBytes";

    /// <summary>
    /// <see cref="AllowedContentTypesContributor"/>: the allowed MIME types (<see cref="string"/>[]).
    /// </summary>
    public const string AllowedContentTypes = Prefix + "AllowedContentTypes.ContentTypes";

    /// <summary>
    /// <see cref="AllowedContentTypesContributor"/>: whether unidentified content is allowed (<see cref="bool"/>).
    /// </summary>
    public const string AllowUnidentified = Prefix + "AllowedContentTypes.AllowUnidentified";

    /// <summary>
    /// <see cref="GZipContributor"/>: the <see cref="System.IO.Compression.CompressionLevel"/>.
    /// </summary>
    public const string GZipCompressionLevel = Prefix + "GZip.CompressionLevel";
}
