using System.IO;
using System.Linq;
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

    [Fact]
    public async Task The_Images_Preset_Should_Store_A_Resized_Picture_And_Record_It()
    {
        var upload = BlobManagementTestFiles.Png(256, 128, seed: 3);

        var descriptor = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(upload);
            return await _fileDescriptorManager.CreateAsync(BlobManagementTestContainers.Images, "wide.png", stream);
        });

        var stored = await WithUnitOfWorkAsync(async () =>
        {
            await using var stream = await _fileDescriptorManager.GetStreamOrNullAsync(descriptor);
            return BlobManagementTestFiles.ReadAll(stream!);
        });

        BlobManagementTestFiles.Measure(stored).ShouldBe((BlobManagementTestContainers.ImagesMaxWidth, BlobManagementTestContainers.ImagesMaxWidth / 2));
        descriptor.MimeType.ShouldBe("image/png");
        descriptor.Size.ShouldBe(stored.Length);
        descriptor.Size.ShouldBeLessThan(upload.Length);
    }

    [Fact]
    public async Task The_Images_Preset_Should_Refuse_What_Is_Not_A_Picture()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Pdf("not a picture"));
            return await _fileDescriptorManager.CreateAsync(BlobManagementTestContainers.Images, "notes.pdf", stream);
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
            typeof(ImageMetadataStripContributor),
            typeof(JpegToWebpContributor),
            typeof(ImageResizeContributor)
        });
        container.GetJpegToWebpContributorConfiguration().Quality.ShouldBe(BlobManagementPresetDefaults.ImagesWebpQuality);
        container.GetStoredContentType("image/jpeg").ShouldBe("image/webp");
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
        asUploaded.GetStoredContentType("image/jpeg").ShouldBe("image/jpeg");

        var lower = new BlobContainerConfiguration().UseImagesPreset(o => o.WebpQuality = 60);
        lower.GetJpegToWebpContributorConfiguration().Quality.ShouldBe(60);
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

    /// <summary>
    /// A JPEG uploaded to a user drive is stored as WebP: the physical BLOB is named <c>.webp</c>, the descriptor records
    /// the WebP, and the file's name takes the <c>.webp</c> extension, so a download is named for what it is.
    /// </summary>
    [Fact]
    public async Task A_Jpeg_Uploaded_To_A_User_BlobManagement_Should_Be_Stored_And_Named_As_Webp()
    {
        var descriptor = await UploadAsync(BlobManagementTestContainers.UserImages, "holiday.jpg", BlobManagementTestFiles.Jpeg(128, 96, seed: 5));

        var stored = await ReadStoredAsync(descriptor);
        BlobManagementTestFiles.Format(stored).ShouldBe(SkiaSharp.SKEncodedImageFormat.Webp);
        BlobManagementTestFiles.Measure(stored).ShouldBe((BlobManagementTestContainers.ImagesMaxWidth, 48));
        descriptor.BlobName.ShouldEndWith(".webp");
        descriptor.MimeType.ShouldBe("image/webp");
        descriptor.Name.ShouldBe("holiday.webp");
        descriptor.Size.ShouldBe(stored.Length);
    }

    /// <summary>
    /// In a system container the name is the application's address, so it is kept; only the physical BLOB is <c>.webp</c>,
    /// and the descriptor records what is served.
    /// </summary>
    [Fact]
    public async Task A_Jpeg_Stored_In_A_System_Container_Should_Keep_Its_Name()
    {
        var descriptor = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(BlobManagementTestFiles.Jpeg(48, 32, seed: 6));
            return await _fileDescriptorManager.CreateAsync(BlobManagementTestContainers.Images, "covers/holiday.jpg", stream);
        });

        var stored = await ReadStoredAsync(descriptor);
        BlobManagementTestFiles.Format(stored).ShouldBe(SkiaSharp.SKEncodedImageFormat.Webp);
        descriptor.Name.ShouldBe("covers/holiday.jpg");
        descriptor.BlobName.ShouldEndWith(".webp");
        descriptor.MimeType.ShouldBe("image/webp");
        (await WithUnitOfWorkAsync(() => _fileDescriptorManager.FindAsync(BlobManagementTestContainers.Images, "covers/holiday.jpg")))
            .ShouldNotBeNull().Id.ShouldBe(descriptor.Id);
    }

    /// <summary>
    /// Dedup compares the upload's hash, so the same JPEG uploaded again shares the WebP stored the first time - and its
    /// name, too, takes the extension of what is shared.
    /// </summary>
    [Fact]
    public async Task The_Same_Jpeg_Uploaded_Again_Should_Share_The_Webp_And_Be_Named_For_It()
    {
        var upload = BlobManagementTestFiles.Jpeg(40, 40, seed: 7);

        var first = await UploadAsync(BlobManagementTestContainers.UserImages, "a.jpg", upload);
        var second = await UploadAsync(BlobManagementTestContainers.UserImages, "b.jpeg", upload);

        second.OwnsBlob().ShouldBeFalse();
        second.BlobName.ShouldBe(first.BlobName);
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
            return await _fileDescriptorManager.CreateAsync(BlobManagementTestContainers.ImagesAsUploaded, "holiday.jpg", stream);
        });

        (await ReadStoredAsync(descriptor)).ShouldBe(upload);
        descriptor.BlobName.ShouldEndWith(".jpg");
        descriptor.MimeType.ShouldBe("image/jpeg");
        descriptor.Name.ShouldBe("holiday.jpg");
    }

    /// <summary>A PNG in a user drive keeps its type and its name.</summary>
    [Fact]
    public async Task A_Png_Uploaded_To_A_User_BlobManagement_Should_Keep_Its_Name()
    {
        var descriptor = await UploadAsync(BlobManagementTestContainers.UserImages, "drawing.png", BlobManagementTestFiles.Png(32, 32, seed: 9));

        descriptor.MimeType.ShouldBe("image/png");
        descriptor.Name.ShouldBe("drawing.png");
        descriptor.BlobName.ShouldEndWith(".png");
    }

    private Task<FileDescriptor> UploadAsync(string containerName, string name, byte[] content)
    {
        return WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(content);
            return await _fileDescriptorManager.UploadAsync(containerName, name, stream);
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
