using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Dignite.Abp.BlobStoring.Pipeline;
using Dignite.Drive.Presets;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Modularity;
using Xunit;

namespace Dignite.Drive;

/// <summary>
/// The container presets: what each adds to a container, and the images preset end to end - a picture stored through
/// <see cref="FileDescriptorManager"/> is resized, and the descriptor records what was stored.
/// </summary>
public abstract class DrivePresets_Tests<TStartupModule> : DriveDomainTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private const long Megabyte = 1024 * 1024;

    private readonly FileDescriptorManager _fileDescriptorManager;

    protected DrivePresets_Tests()
    {
        _fileDescriptorManager = GetRequiredService<FileDescriptorManager>();
    }

    [Fact]
    public async Task The_Images_Preset_Should_Store_A_Resized_Picture_And_Record_It()
    {
        var upload = DriveTestFiles.Png(256, 128, seed: 3);

        var descriptor = await WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(upload);
            return await _fileDescriptorManager.CreateAsync(DriveTestContainers.Images, "wide.png", stream);
        });

        var stored = await WithUnitOfWorkAsync(async () =>
        {
            await using var stream = await _fileDescriptorManager.GetStreamOrNullAsync(descriptor);
            return DriveTestFiles.ReadAll(stream!);
        });

        DriveTestFiles.Measure(stored).ShouldBe((DriveTestContainers.ImagesMaxWidth, DriveTestContainers.ImagesMaxWidth / 2));
        descriptor.MimeType.ShouldBe("image/png");
        descriptor.Size.ShouldBe(stored.Length);
        descriptor.Size.ShouldBeLessThan(upload.Length);
    }

    [Fact]
    public async Task The_Images_Preset_Should_Refuse_What_Is_Not_A_Picture()
    {
        var exception = await Should.ThrowAsync<BusinessException>(() => WithUnitOfWorkAsync(async () =>
        {
            using var stream = new MemoryStream(DriveTestFiles.Pdf("not a picture"));
            return await _fileDescriptorManager.CreateAsync(DriveTestContainers.Images, "notes.pdf", stream);
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
            typeof(ImageResizeContributor)
        });
        container.GetMaxSizeContributorConfiguration().MaxSizeInBytes.ShouldBe(DrivePresetDefaults.ImagesMaxSizeInMegabytes * Megabyte);
        container.GetAllowedContentTypesContributorConfiguration().AllowedContentTypes
            .ShouldBe(DriveFileTypes.GetMimeTypes(DriveFileTypes.Images), ignoreOrder: true);
        container.GetImageResizeContributorConfiguration().MaxWidth.ShouldBe(DrivePresetDefaults.ImagesMaxWidth);
        container.GetImageResizeContributorConfiguration().MaxHeight.ShouldBe(DrivePresetDefaults.ImagesMaxHeight);
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

        container.GetMaxSizeContributorConfiguration().MaxSizeInBytes.ShouldBe(DrivePresetDefaults.BinaryAssetsMaxSizeInMegabytes * Megabyte);
        container.GetAllowedContentTypesContributorConfiguration().AllowedContentTypes
            .ShouldBe(DriveFileTypes.GetMimeTypes(DriveFileTypes.BinaryAssets), ignoreOrder: true);
    }

    [Fact]
    public void The_Any_Preset_Should_Only_Cap_The_Size()
    {
        var container = new BlobContainerConfiguration().UseAnyPreset();

        container.PipelineContributors.ToArray().ShouldBe(new[] { typeof(MaxSizeContributor) });
        container.GetMaxSizeContributorConfiguration().MaxSizeInBytes.ShouldBe(DrivePresetDefaults.AnyMaxSizeInMegabytes * Megabyte);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_Preset_Should_Refuse_A_Size_That_Is_Not_Positive(long megabytes)
    {
        Should.Throw<AbpException>(() => new BlobContainerConfiguration().UseAnyPreset(o => o.MaxSizeInMegabytes = megabytes));
    }
}
