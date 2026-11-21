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
    /// Stores the JPEGs saved to the container as lossy WebP at <paramref name="quality"/> (1-100), turned upright and
    /// fitted to the container's resize box (<see cref="JpegToWebpContributor"/>) - all of them, except one that would
    /// not fit WebP's 16383 pixels on a side. Add it after <c>AddAllowedContentTypesContributor</c> and before
    /// <c>AddImageResizeContributor</c>, whose box it applies. Calling it again only changes the quality.
    /// <para>
    /// It also declares <c>image/jpeg</c> as stored as <c>image/webp</c> under that rule (<c>SetStoredContentType</c>), so
    /// a caller that names its blobs or records their type before the save reads <c>GetStoredContentType</c> and gets it
    /// right.
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

        // The contributor converts what this declares, so the container can say before the save what it stores one as.
        containerConfiguration.SetStoredContentType("image/jpeg", WebpConversion.WebpContentType, WebpConversion.CanStoreJpegAsWebp);
        containerConfiguration.PipelineContributors.TryAdd<JpegToWebpContributor>();
        return containerConfiguration;
    }

    public static PngToWebpContributorConfiguration GetPngToWebpContributorConfiguration(
        this BlobContainerConfiguration containerConfiguration)
    {
        return new PngToWebpContributorConfiguration(containerConfiguration);
    }

    /// <summary>
    /// Stores the PNGs saved to the container as lossless WebP, turned upright and fitted to the container's resize box
    /// (<see cref="PngToWebpContributor"/>) - except one smaller than <paramref name="minSizeInBytes"/>, with 16 bits per
    /// channel, animated, or that would not fit WebP's 16383 pixels on a side, which is stored as it is. Add it after
    /// <c>AddAllowedContentTypesContributor</c> and before any contributor that changes the bytes
    /// (<c>AddImageMetadataStripContributor</c>, <c>AddImageResizeContributor</c>): the rule reads the length, which has to
    /// be the one the caller saves. Calling it again only changes the size.
    /// <para>
    /// It also declares <c>image/png</c> as stored as <c>image/webp</c> under that rule (<c>SetStoredContentType</c>), so a
    /// caller that names its blobs or records their type before the save reads <c>GetStoredContentType</c> and gets it
    /// right.
    /// </para>
    /// </summary>
    public static BlobContainerConfiguration StorePngAsWebp(
        this BlobContainerConfiguration containerConfiguration,
        long minSizeInBytes = PngToWebpContributorConfiguration.DefaultMinSizeInBytes)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));

        var configuration = containerConfiguration.GetPngToWebpContributorConfiguration();
        configuration.MinSizeInBytes = minSizeInBytes;
        configuration.Validate();

        containerConfiguration.SetStoredContentType("image/png", WebpConversion.WebpContentType, WebpConversion.CanStorePngAsWebp);
        containerConfiguration.PipelineContributors.TryAdd<PngToWebpContributor>();
        return containerConfiguration;
    }
}
