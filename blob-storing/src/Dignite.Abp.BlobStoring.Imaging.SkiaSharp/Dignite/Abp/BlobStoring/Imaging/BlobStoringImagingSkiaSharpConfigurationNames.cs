namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The <see cref="Volo.Abp.BlobStoring.BlobContainerConfiguration"/> entries the SkiaSharp image contributors read.
/// Prefer the typed wrappers (<see cref="JpegToWebpContributorConfiguration"/>, <see cref="PngToWebpContributorConfiguration"/>)
/// over setting these directly.
/// </summary>
public static class BlobStoringImagingSkiaSharpConfigurationNames
{
    private const string Prefix = "Dignite.Abp.BlobStoring.Imaging.SkiaSharp.";

    /// <summary>
    /// <see cref="JpegToWebpContributor"/>: the quality, 1-100, of the WebP a JPEG is stored as (<see cref="int"/>).
    /// </summary>
    public const string JpegToWebpQuality = Prefix + "JpegToWebp.Quality";

    /// <summary>
    /// <see cref="PngToWebpContributor"/>: the size, in bytes, from which a PNG is stored as WebP (<see cref="long"/>).
    /// </summary>
    public const string PngToWebpMinSizeInBytes = Prefix + "PngToWebp.MinSizeInBytes";
}
