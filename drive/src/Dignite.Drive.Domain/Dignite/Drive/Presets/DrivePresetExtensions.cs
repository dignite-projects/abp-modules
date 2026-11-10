using System;
using System.Linq;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Drive.Presets;

/// <summary>
/// Ready-made upload rules for a Drive container: each preset adds blob-storing's pipeline contributors in the order
/// they have to run, with numeric parameters only. A container takes one preset; contributors added after it adjust it
/// (calling an <c>Add…Contributor</c> again updates that contributor's configuration).
/// <para>
/// The allowed types come from <see cref="DriveFileTypes"/>, which leaves out SVG, HTML and scripts: a container may be
/// read publicly. <see cref="UseAnyPreset"/> allows every type, so do not combine it with <c>PublicRead</c>.
/// </para>
/// </summary>
public static class DrivePresetExtensions
{
    private const long BytesPerMegabyte = 1024L * 1024L;

    /// <summary>
    /// Raster pictures (<see cref="DriveFileTypes.Images"/>): a size cap, the allowed types, metadata removal (EXIF
    /// location and the like) and a resize into <see cref="DriveImagesPresetOptions.MaxWidth"/> ×
    /// <see cref="DriveImagesPresetOptions.MaxHeight"/>. The host needs one of ABP's imaging providers
    /// (<c>Volo.Abp.Imaging.SkiaSharp</c>, …) for the resize; without one, pictures are stored as they are.
    /// </summary>
    public static BlobContainerConfiguration UseImagesPreset(
        this BlobContainerConfiguration containerConfiguration,
        Action<DriveImagesPresetOptions>? configureAction = null)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));

        var options = new DriveImagesPresetOptions();
        configureAction?.Invoke(options);
        if (options.MaxWidth < 0 || options.MaxHeight < 0)
        {
            throw new AbpException($"The images preset's {nameof(options.MaxWidth)} and {nameof(options.MaxHeight)} cannot be negative.");
        }

        AddSizeCap(containerConfiguration, options);
        containerConfiguration.AddAllowedContentTypesContributor(c =>
            c.AllowedContentTypes = DriveFileTypes.GetMimeTypes(DriveFileTypes.Images));
        containerConfiguration.AddImageMetadataStripContributor();
        if (options.MaxWidth > 0 || options.MaxHeight > 0)
        {
            containerConfiguration.AddImageResizeContributor(c =>
            {
                c.MaxWidth = options.MaxWidth;
                c.MaxHeight = options.MaxHeight;
            });
        }

        return containerConfiguration;
    }

    /// <summary>
    /// Documents and archives (<see cref="DriveFileTypes.Documents"/>, <see cref="DriveFileTypes.Archives"/>): a size cap
    /// and the allowed types.
    /// </summary>
    public static BlobContainerConfiguration UseDocumentsPreset(
        this BlobContainerConfiguration containerConfiguration,
        Action<DriveSizePresetOptions>? configureAction = null)
    {
        return UseTypedPreset(
            containerConfiguration,
            DrivePresetDefaults.DocumentsMaxSizeInMegabytes,
            DriveFileTypes.GetMimeTypes(DriveFileTypes.Documents.Concat(DriveFileTypes.Archives)),
            configureAction);
    }

    /// <summary>
    /// What a web page links (<see cref="DriveFileTypes.BinaryAssets"/>: an icon, web fonts): a size cap and the allowed
    /// types, stored as they are.
    /// </summary>
    public static BlobContainerConfiguration UseBinaryAssetsPreset(
        this BlobContainerConfiguration containerConfiguration,
        Action<DriveSizePresetOptions>? configureAction = null)
    {
        return UseTypedPreset(
            containerConfiguration,
            DrivePresetDefaults.BinaryAssetsMaxSizeInMegabytes,
            DriveFileTypes.GetMimeTypes(DriveFileTypes.BinaryAssets),
            configureAction);
    }

    /// <summary>A size cap and nothing else: every type is accepted.</summary>
    public static BlobContainerConfiguration UseAnyPreset(
        this BlobContainerConfiguration containerConfiguration,
        Action<DriveSizePresetOptions>? configureAction = null)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));

        var options = new DriveSizePresetOptions(DrivePresetDefaults.AnyMaxSizeInMegabytes);
        configureAction?.Invoke(options);
        AddSizeCap(containerConfiguration, options);
        return containerConfiguration;
    }

    private static BlobContainerConfiguration UseTypedPreset(
        BlobContainerConfiguration containerConfiguration,
        long defaultMaxSizeInMegabytes,
        string[] allowedContentTypes,
        Action<DriveSizePresetOptions>? configureAction)
    {
        Check.NotNull(containerConfiguration, nameof(containerConfiguration));

        var options = new DriveSizePresetOptions(defaultMaxSizeInMegabytes);
        configureAction?.Invoke(options);
        AddSizeCap(containerConfiguration, options);
        containerConfiguration.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = allowedContentTypes);
        return containerConfiguration;
    }

    private static void AddSizeCap(BlobContainerConfiguration containerConfiguration, DriveSizePresetOptions options)
    {
        if (options.MaxSizeInMegabytes <= 0 || options.MaxSizeInMegabytes > Array.MaxLength / BytesPerMegabyte)
        {
            throw new AbpException(
                $"A preset's {nameof(options.MaxSizeInMegabytes)} must be between 1 and {Array.MaxLength / BytesPerMegabyte}.");
        }

        containerConfiguration.AddMaxSizeContributor(c => c.MaxSizeInBytes = options.MaxSizeInMegabytes * BytesPerMegabyte);
    }
}
