using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Adds the SkiaSharp image contributors to a container. Named apart from <c>ImagingBlobContainerConfigurationExtensions</c>
/// so a file importing both can name either.
/// </summary>
public static class SkiaSharpImagingBlobContainerConfigurationExtensions
{
    public static JpegToWebpContributorConfiguration GetJpegToWebpContributorConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new JpegToWebpContributorConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Stores every JPEG saved to the container as lossy WebP at <paramref name="quality"/> (1-100), turned upright and
    /// fitted to the container's resize box (<see cref="JpegToWebpContributor"/>). Add it after
    /// <c>AddAllowedContentTypesContributor</c> and <c>AddImageMetadataStripContributor</c>, and before
    /// <c>AddImageResizeContributor</c>, whose box it applies. Calling it again only changes the quality.
    /// <para>
    /// It also declares <c>image/jpeg</c> as stored as <c>image/webp</c> (<c>SetStoredContentType</c>), so a caller that
    /// names its blobs or records their type before the save reads <c>GetStoredContentType</c> and gets it right.
    /// </para>
    /// </summary>
    public static BlobContainerConfiguration StoreJpegAsWebp(
        this BlobContainerConfiguration containerConfiguration,
        int quality = JpegToWebpContributorConfiguration.DefaultQuality)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));

        var configuration = containerConfiguration.GetJpegToWebpContributorConfiguration();
        configuration.Quality = quality;
        configuration.Validate();

        // Every JPEG is converted, so the container can say before the save what it stores one as.
        containerConfiguration.SetStoredContentType("image/jpeg", "image/webp");
        containerConfiguration.PipelineContributors.TryAdd<JpegToWebpContributor>();
        return containerConfiguration;
    }
}
