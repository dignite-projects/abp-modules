namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The <see cref="Volo.Abp.BlobStoring.BlobContainerConfiguration"/> entries the SkiaSharp image contributors read.
/// Prefer the typed wrapper (<see cref="JpegToWebpContributorConfiguration"/>) over setting these directly.
/// </summary>
public static class BlobStoringImagingSkiaSharpConfigurationNames
{
    private const string Prefix = "Dignite.Abp.BlobStoring.Imaging.SkiaSharp.";

    /// <summary>
    /// <see cref="JpegToWebpContributor"/>: the quality, 1-100, of the WebP a JPEG is stored as (<see cref="int"/>).
    /// </summary>
    public const string JpegToWebpQuality = Prefix + "JpegToWebp.Quality";
}
