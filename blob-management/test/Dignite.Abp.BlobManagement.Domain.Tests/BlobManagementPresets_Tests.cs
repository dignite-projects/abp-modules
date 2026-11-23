using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Abp.BlobManagement.Presets;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The container presets: what each adds to a container, and the images preset end to end - a picture stored through
/// <see cref="FileDescriptorManager"/> is resized, and the descriptor records what was stored.
/// </summary>
public abstract class BlobManagementPresets_Tests<TStartupModule> : BlobManagementDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const long Megabyte = 1024 * 1024;

    private readonly FileDescriptorManager _fileDescriptorManager;

    protected BlobManagementPresets_Tests()
    {
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
    }

    /// <summary>A PNG left as one - here with the conversions off - is resized and stays a PNG.</summary>
    [Fact]
    public async Task The_Images_Preset_Should_Store_A_Resized_Picture_And_Record_It()
    {
        var upload = BlobManagementTestFiles.Png(256, 128, seed: 3);

        var descriptor = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(upload);
            return await _fileDescriptorManager.SaveBlobAsync(BlobManagementTestContainers.ImagesAsUploaded, "wide.png", stream);
        });

        var stored = await ReadStoredAsync(descriptor);

        BlobManagementTestFiles.Measure(stored).ShouldBe((BlobManagementTestContainers.ImagesMaxWidth, BlobManagementTestContainers.ImagesMaxWidth / 2));
        BlobManagementTestFiles.Format(stored).ShouldBe(SkiaSharp.SKEncodedImageFormat.Png);
        descriptor.MimeType.ShouldBe("image/png");
        descriptor.BlobName.ShouldEndWith(".png");
        descriptor.Size.ShouldBe(stored.Length);
        descriptor.Size.ShouldBeLessThan(upload.Length);
    }

    /// <summary>
    /// A PNG of 64 KB or more is stored as a lossless WebP, fitted into the box: the physical BLOB is named <c>.webp</c>,
    /// the descriptor records the WebP, and the name of a file kept by id takes the extension.
    /// </summary>
    [Fact]
    public async Task A_Png_Kept_By_Id_Should_Be_Stored_And_Named_As_Webp()
    {
        var upload = BlobManagementTestFiles.Png(256, 128, seed: 3);
        upload.Length.ShouldBeGreaterThanOrEqualTo((int)BlobManagementPresetDefaults.ImagesPngWebpMinSizeInBytes);

        var descriptor = await CreateFileAsync(BlobManagementTestContainers.UserImages, "diagram.png", upload);

        var stored = await ReadStoredAsync(descriptor);
        BlobManagementTestFiles.Format(stored).ShouldBe(SkiaSharp.SKEncodedImageFormat.Webp);
        BlobManagementTestFiles.Measure(stored).ShouldBe((BlobManagementTestContainers.ImagesMaxWidth, BlobManagementTestContainers.ImagesMaxWidth / 2));
        descriptor.BlobName.ShouldEndWith(".webp");
        descriptor.MimeType.ShouldBe("image/webp");
        descriptor.Name.ShouldBe("diagram.webp");
        descriptor.Size.ShouldBe(stored.Length);
    }

    /// <summary>
    /// An APNG keeps its frames: it is neither converted nor resized, and its BLOB is named for what is stored - the
    /// declaration read before the save agrees with the contributors.
    /// </summary>
    [Fact]
    public async Task An_Animated_Png_Should_Be_Stored_As_Uploaded()
    {
        var upload = BlobManagementTestFiles.WithAnimationControl(BlobManagementTestFiles.Png(256, 128, seed: 4));

        var descriptor = await CreateFileAsync(BlobManagementTestContainers.UserImages, "spinner.png", upload);

        (await ReadStoredAsync(descriptor)).ShouldBe(upload);
        descriptor.BlobName.ShouldEndWith(".png");
        descriptor.MimeType.ShouldBe("image/png");
        descriptor.Name.ShouldBe("spinner.png");
    }

    [Fact]
    public async Task The_Images_Preset_Should_Refuse_What_Is_Not_A_Picture()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Pdf("not a picture"));
            return await _fileDescriptorManager.SaveBlobAsync(BlobManagementTestContainers.Images, "notes.pdf", stream);
        }));

        exception.Code.ShouldBe(BlobStoringPipelineErrorCodes.ContentTypeNotAllowed);
    }

    [Fact]
    public void The_Images_Preset_Should_Add_Its_Contributors_In_Order()
    {
        var container = new BlobContainerConfiguration().UseImagesPreset();

        container.PipelineContributors.ToArray().ShouldBe(new[]
        {
            typeof(MaxSizeContributor),
            typeof(AllowedContentTypesContributor),
            typeof(JpegToWebpContributor),
            typeof(PngToWebpContributor),
            typeof(ImageMetadataStripContributor),
            typeof(ImageResizeContributor)
        });
        container.GetJpegToWebpContributorConfiguration().Quality.ShouldBe(BlobManagementPresetDefaults.ImagesWebpQuality);
        container.GetPngToWebpContributorConfiguration().MinSizeInBytes.ShouldBe(BlobManagementPresetDefaults.ImagesPngWebpMinSizeInBytes);
        using (var jpeg = new MemoryStream(BlobManagementTestFiles.Jpeg(40, 40)))
        {
            container.GetStoredContentType("image/jpeg", jpeg).ShouldBe("image/webp");
        }
        container.GetMaxSizeContributorConfiguration().MaxSizeInBytes.ShouldBe(BlobManagementPresetDefaults.ImagesMaxSizeInMegabytes * Megabyte);
        container.GetAllowedContentTypesContributorConfiguration().AllowedContentTypes
            .ShouldBe(BlobManagementFileTypes.GetMimeTypes(BlobManagementFileTypes.Images), ignoreOrder: true);
        container.GetImageResizeContributorConfiguration().MaxWidth.ShouldBe(BlobManagementPresetDefaults.ImagesMaxWidth);
        container.GetImageResizeContributorConfiguration().MaxHeight.ShouldBe(BlobManagementPresetDefaults.ImagesMaxHeight);
    }

    [Fact]
    public void The_Images_Preset_Without_Dimensions_Should_Not_Resize()
    {
        var container = new BlobContainerConfiguration().UseImagesPreset(o =>
        {
            o.MaxWidth = 0;
            o.MaxHeight = 0;
        });

        container.PipelineContributors.ShouldNotContain(typeof(ImageResizeContributor));
    }

    [Fact]
    public void The_Images_Preset_Can_Store_Jpegs_As_Uploaded_Or_At_Another_Quality()
    {
        var asUploaded = new BlobContainerConfiguration().UseImagesPreset(o => o.StoreJpegAsWebp = false);
        asUploaded.PipelineContributors.ShouldNotContain(typeof(JpegToWebpContributor));
        using var jpeg = new MemoryStream(BlobManagementTestFiles.Jpeg(40, 40));
        asUploaded.GetStoredContentType("image/jpeg", jpeg).ShouldBe("image/jpeg");

        var lower = new BlobContainerConfiguration().UseImagesPreset(o => o.WebpQuality = 60);
        lower.GetJpegToWebpContributorConfiguration().Quality.ShouldBe(60);
    }

    [Fact]
    public void The_Images_Preset_Can_Store_Pngs_As_Uploaded_Or_From_Another_Size()
    {
        var asUploaded = new BlobContainerConfiguration().UseImagesPreset(o => o.StorePngAsWebp = false);
        asUploaded.PipelineContributors.ShouldNotContain(typeof(PngToWebpContributor));
        using var png = new MemoryStream(BlobManagementTestFiles.Png(256, 128));
        asUploaded.GetStoredContentType("image/png", png).ShouldBe("image/png");

        var larger = new BlobContainerConfiguration().UseImagesPreset(o => o.PngWebpMinSizeInBytes = 1024 * 1024);
        larger.GetPngToWebpContributorConfiguration().MinSizeInBytes.ShouldBe(1024 * 1024);
        larger.GetStoredContentType("image/png", png).ShouldBe("image/png");
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 101)]
    public void The_Images_Preset_Should_Refuse_A_Webp_Quality_Out_Of_Range(bool storeJpegAsWebp, int quality)
    {
        Should.Throw<AbpException>(() => new BlobContainerConfiguration().UseImagesPreset(o =>
        {
            o.StoreJpegAsWebp = storeJpegAsWebp;
            o.WebpQuality = quality;
        }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_Images_Preset_Should_Refuse_A_Negative_Png_Size(bool storePngAsWebp)
    {
        Should.Throw<AbpException>(() => new BlobContainerConfiguration().UseImagesPreset(o =>
        {
            o.StorePngAsWebp = storePngAsWebp;
            o.PngWebpMinSizeInBytes = -1;
        }));
    }

    /// <summary>
    /// A JPEG kept by id is stored as WebP: the physical BLOB is named <c>.webp</c>, the descriptor records the WebP,
    /// and the file's name takes the <c>.webp</c> extension, so a download is named for what it is.
    /// </summary>
    [Fact]
    public async Task A_Jpeg_Kept_By_Id_Should_Be_Stored_And_Named_As_Webp()
    {
        var descriptor = await CreateFileAsync(BlobManagementTestContainers.UserImages, "holiday.jpg", BlobManagementTestFiles.Jpeg(128, 96, seed: 5));

        var stored = await ReadStoredAsync(descriptor);
        BlobManagementTestFiles.Format(stored).ShouldBe(SkiaSharp.SKEncodedImageFormat.Webp);
        BlobManagementTestFiles.Measure(stored).ShouldBe((BlobManagementTestContainers.ImagesMaxWidth, 48));
        descriptor.BlobName.ShouldEndWith(".webp");
        descriptor.MimeType.ShouldBe("image/webp");
        descriptor.Name.ShouldBe("holiday.webp");
        descriptor.Size.ShouldBe(stored.Length);
    }

    /// <summary>
    /// A BLOB saved under a name keeps it, the name being the application's address; only the physical BLOB is <c>.webp</c>,
    /// and the descriptor records what is served.
    /// </summary>
    [Fact]
    public async Task A_Jpeg_Saved_Under_A_Name_Should_Keep_Its_Name()
    {
        var descriptor = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Jpeg(48, 32, seed: 6));
            return await _fileDescriptorManager.SaveBlobAsync(BlobManagementTestContainers.Images, "covers/holiday.jpg", stream);
        });

        var stored = await ReadStoredAsync(descriptor);
        BlobManagementTestFiles.Format(stored).ShouldBe(SkiaSharp.SKEncodedImageFormat.Webp);
        descriptor.Name.ShouldBe("covers/holiday.jpg");
        descriptor.BlobName.ShouldEndWith(".webp");
        descriptor.MimeType.ShouldBe("image/webp");
        (await WithUnitOfWorkAsync(() => _fileDescriptorManager.FindBlobAsync(BlobManagementTestContainers.Images, "covers/holiday.jpg")))
            .ShouldNotBeNull().Id.ShouldBe(descriptor.Id);
    }

    /// <summary>
    /// The same JPEG kept again is converted again, into a WebP of its own, and its name takes that extension too.
    /// </summary>
    [Fact]
    public async Task The_Same_Jpeg_Kept_Twice_Should_Be_Stored_As_A_Webp_Of_Its_Own()
    {
        var upload = BlobManagementTestFiles.Jpeg(40, 40, seed: 7);

        var first = await CreateFileAsync(BlobManagementTestContainers.UserImages, "a.jpg", upload);
        var second = await CreateFileAsync(BlobManagementTestContainers.UserImages, "b.jpeg", upload);

        second.BlobName.ShouldNotBe(first.BlobName);
        second.BlobName.ShouldEndWith(".webp");
        second.MimeType.ShouldBe("image/webp");
        second.Name.ShouldBe("b.webp");
    }

    [Fact]
    public async Task With_Webp_Turned_Off_A_Jpeg_Should_Be_Stored_As_Uploaded()
    {
        var upload = BlobManagementTestFiles.Jpeg(48, 32, seed: 8);

        var descriptor = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(upload);
            return await _fileDescriptorManager.SaveBlobAsync(BlobManagementTestContainers.ImagesAsUploaded, "holiday.jpg", stream);
        });

        (await ReadStoredAsync(descriptor)).ShouldBe(upload);
        descriptor.BlobName.ShouldEndWith(".jpg");
        descriptor.MimeType.ShouldBe("image/jpeg");
        descriptor.Name.ShouldBe("holiday.jpg");
    }

    /// <summary>A PNG kept by id under 64 KB, too small to convert, keeps its type and its name.</summary>
    [Fact]
    public async Task A_Small_Png_Kept_By_Id_Should_Keep_Its_Name()
    {
        var upload = BlobManagementTestFiles.Png(32, 32, seed: 9);
        upload.Length.ShouldBeLessThan((int)BlobManagementPresetDefaults.ImagesPngWebpMinSizeInBytes);

        var descriptor = await CreateFileAsync(BlobManagementTestContainers.UserImages, "drawing.png", upload);

        descriptor.MimeType.ShouldBe("image/png");
        descriptor.Name.ShouldBe("drawing.png");
        descriptor.BlobName.ShouldEndWith(".png");
    }

    /// <summary>
    /// The checksum is of the bytes the caller sent, taken before the container's contributors converted them: the
    /// hash of the JPEG, though a WebP is stored.
    /// </summary>
    [Fact]
    public async Task The_Checksum_Of_A_Converted_Picture_Should_Be_That_Of_What_The_Caller_Sent()
    {
        var jpeg = BlobManagementTestFiles.Jpeg(40, 40, seed: 11);
        var checksum = System.Convert.ToBase64String(SHA256.HashData(jpeg));

        var descriptor = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(jpeg);
            return await _fileDescriptorManager.CreateFileAsync(
                BlobManagementTestContainers.UserImages, "checked.jpg", stream, new CreateFileArgs { ExpectedSha256 = checksum });
        });

        descriptor.MimeType.ShouldBe("image/webp");
        descriptor.Hash.ShouldBe(checksum);
    }

    /// <summary>
    /// The box an upload asks for is applied with the container's in the one encode: a JPEG on the images preset is stored
    /// as a WebP fitted inside it, its aspect ratio kept, and named for it.
    /// </summary>
    [Fact]
    public async Task An_Upload_Should_Store_Its_Pictures_Within_The_Box_It_Asks_For()
    {
        var descriptor = await CreateFileAsync(
            BlobManagementTestContainers.UserImages, "holiday.jpg", BlobManagementTestFiles.Jpeg(128, 96, seed: 61),
            new CreateFileArgs { ImageMaxWidth = 32, ImageMaxHeight = 32 });

        var stored = await ReadStoredAsync(descriptor);
        BlobManagementTestFiles.Format(stored).ShouldBe(SkiaSharp.SKEncodedImageFormat.Webp);
        BlobManagementTestFiles.Measure(stored).ShouldBe((32, 24));
        descriptor.BlobName.ShouldEndWith(".webp");
        descriptor.Name.ShouldBe("holiday.webp");
        descriptor.Size.ShouldBe(stored.Length);
    }

    /// <summary>An upload makes what the container keeps smaller, never larger: each side is the smaller of the two.</summary>
    [Fact]
    public async Task An_Upload_Box_Should_Not_Enlarge_The_Containers()
    {
        var descriptor = await CreateFileAsync(
            BlobManagementTestContainers.UserImages, "holiday.jpg", BlobManagementTestFiles.Jpeg(128, 96, seed: 62),
            new CreateFileArgs { ImageMaxWidth = 1000, ImageMaxHeight = 1000 });

        BlobManagementTestFiles.Measure(await ReadStoredAsync(descriptor)).ShouldBe((BlobManagementTestContainers.ImagesMaxWidth, 48));
    }

    /// <summary>A box with one side constrains that side, and the picture keeps its aspect ratio.</summary>
    [Fact]
    public async Task An_Upload_Box_With_One_Side_Should_Constrain_That_Side()
    {
        var descriptor = await CreateFileAsync(
            BlobManagementTestContainers.UserImages, "holiday.jpg", BlobManagementTestFiles.Jpeg(128, 96, seed: 63),
            new CreateFileArgs { ImageMaxHeight = 12 });

        BlobManagementTestFiles.Measure(await ReadStoredAsync(descriptor)).ShouldBe((16, 12));
    }

    /// <summary>
    /// A container that resizes nothing resizes an upload that asks for a box: a larger picture is scaled down and stays
    /// a PNG, one that fits is stored byte for byte, and what is not a picture passes through.
    /// </summary>
    [Fact]
    public async Task A_Container_That_Does_Not_Resize_Should_Fit_An_Upload_Into_Its_Box()
    {
        var box = new CreateFileArgs { ImageMaxWidth = 64, ImageMaxHeight = 64 };

        var wide = await CreateFileAsync(BlobManagementTestContainers.Files, "wide.png", BlobManagementTestFiles.Png(256, 128, seed: 64), box);
        var stored = await ReadStoredAsync(wide);
        BlobManagementTestFiles.Format(stored).ShouldBe(SkiaSharp.SKEncodedImageFormat.Png);
        BlobManagementTestFiles.Measure(stored).ShouldBe((64, 32));
        wide.Size.ShouldBe(stored.Length);

        var small = BlobManagementTestFiles.Png(20, 10, seed: 65);
        (await ReadStoredAsync(await CreateFileAsync(BlobManagementTestContainers.Files, "small.png", small, box))).ShouldBe(small);

        var pdf = BlobManagementTestFiles.Pdf("not a picture " + System.Guid.NewGuid());
        (await ReadStoredAsync(await CreateFileAsync(BlobManagementTestContainers.Files, "notes.pdf", pdf, box))).ShouldBe(pdf);
    }

    /// <summary>
    /// A container that stores its content compressed still fits a picture into the upload's box: the resize runs on
    /// the picture, before the compression.
    /// </summary>
    [Fact]
    public async Task A_Container_That_Compresses_Should_Fit_An_Upload_Into_Its_Box_First()
    {
        var descriptor = await CreateFileAsync(
            BlobManagementTestContainers.Zipped, "wide.png", BlobManagementTestFiles.Png(256, 128, seed: 66),
            new CreateFileArgs { ImageMaxWidth = 64, ImageMaxHeight = 64 });

        BlobManagementTestFiles.Measure(await ReadStoredAsync(descriptor)).ShouldBe((64, 32));
    }

    /// <summary>
    /// A container that crops, stretches or pads its pictures has no box an upload could narrow without changing what
    /// the container makes of them: an upload that asks for one is refused, and nothing is stored.
    /// </summary>
    [Fact]
    public async Task A_Container_That_Does_Not_Fit_Its_Pictures_Should_Refuse_An_Upload_Box()
    {
        (await Should.ThrowAsync<BusinessException>(() => CreateFileAsync(
                BlobManagementTestContainers.Cropped, "wide.png", BlobManagementTestFiles.Png(256, 128, seed: 67),
                new CreateFileArgs { ImageMaxWidth = 32 })))
            .Code.ShouldBe(BlobManagementErrorCodes.ImageSizeNotSupported);

        // Without a box the container crops as it is configured to.
        var cropped = await CreateFileAsync(
            BlobManagementTestContainers.Cropped, "wide.png", BlobManagementTestFiles.Png(256, 128, seed: 68));
        BlobManagementTestFiles.Measure(await ReadStoredAsync(cropped))
            .ShouldBe((BlobManagementTestContainers.ImagesMaxWidth, BlobManagementTestContainers.ImagesMaxWidth));
    }

    [Fact]
    public void An_Upload_Box_That_Is_Not_Positive_Should_Be_Refused()
    {
        Should.Throw<System.ArgumentException>(() =>
            _fileDescriptorManager.GetSaveConfiguration(BlobManagementTestContainers.Files, 0, null));
        Should.Throw<System.ArgumentException>(() =>
            _fileDescriptorManager.GetSaveConfiguration(BlobManagementTestContainers.Files, null, -1));
    }

    /// <summary>Without a box, a save runs with the container's own configuration.</summary>
    [Fact]
    public void A_Save_Without_A_Box_Should_Use_The_Containers_Configuration()
    {
        _fileDescriptorManager.GetSaveConfiguration(BlobManagementTestContainers.UserImages, null, null)
            .ShouldBeSameAs(GetRequiredService<IBlobContainerConfigurationProvider>().Get(BlobManagementTestContainers.UserImages));
    }

    private Task<FileDescriptor> CreateFileAsync(string containerName, string name, byte[] content, CreateFileArgs? args = null)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(content);
            return await _fileDescriptorManager.CreateFileAsync(containerName, name, stream, args);
        });
    }

    private Task<byte[]> ReadStoredAsync(FileDescriptor descriptor)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            await using var stream = await _fileDescriptorManager.GetStreamOrNullAsync(descriptor);
            return BlobManagementTestFiles.ReadAll(stream!);
        });
    }

    [Fact]
    public void The_Documents_Preset_Should_Take_Documents_And_Archives()
    {
        var container = new BlobContainerConfiguration().UseDocumentsPreset(o => o.MaxSizeInMegabytes = 3);

        container.GetMaxSizeContributorConfiguration().MaxSizeInBytes.ShouldBe(3 * Megabyte);
        var allowed = container.GetAllowedContentTypesContributorConfiguration().AllowedContentTypes;
        allowed.ShouldContain("application/pdf");
        allowed.ShouldContain("application/zip");
        allowed.ShouldNotContain("image/png");
    }

    [Fact]
    public void The_Binary_Assets_Preset_Should_Take_Icons_And_Fonts()
    {
        var container = new BlobContainerConfiguration().UseBinaryAssetsPreset();

        container.GetMaxSizeContributorConfiguration().MaxSizeInBytes.ShouldBe(BlobManagementPresetDefaults.BinaryAssetsMaxSizeInMegabytes * Megabyte);
        container.GetAllowedContentTypesContributorConfiguration().AllowedContentTypes
            .ShouldBe(BlobManagementFileTypes.GetMimeTypes(BlobManagementFileTypes.BinaryAssets), ignoreOrder: true);
    }

    [Fact]
    public void The_Any_Preset_Should_Only_Cap_The_Size()
    {
        var container = new BlobContainerConfiguration().UseAnyPreset();

        container.PipelineContributors.ToArray().ShouldBe(new[] { typeof(MaxSizeContributor) });
        container.GetMaxSizeContributorConfiguration().MaxSizeInBytes.ShouldBe(BlobManagementPresetDefaults.AnyMaxSizeInMegabytes * Megabyte);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_Preset_Should_Refuse_A_Size_That_Is_Not_Positive(long megabytes)
    {
        Should.Throw<AbpException>(() => new BlobContainerConfiguration().UseAnyPreset(o => o.MaxSizeInMegabytes = megabytes));
    }
}
