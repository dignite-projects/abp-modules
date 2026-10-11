using System;
using System.Threading.Tasks;
using Shouldly;
using SkiaSharp;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Xunit;

namespace Dignite.Abp.BlobStoring.Imaging;

public class ImageDecodeGuard_Tests : BlobStoringImagingTestBase
{
    [Fact]
    public async Task Should_Reject_An_Image_Whose_Header_Declares_A_Huge_Canvas()
    {
        const string containerName = BlobStoringImagingTestModule.ResizeContainer;

        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(containerName).SaveAsync("bomb.png", TestHeaders.Png(100_000, 100_000)));

        exception.Code.ShouldBe(BlobStoringImagingErrorCodes.ImageTooLarge);
        exception.Data["Width"].ShouldBe(100_000);
        exception.Data["MaxWidth"].ShouldBe(ImageDecodeGuardConfiguration.DefaultMaxSourceWidth);
        exception.Data["MaxHeight"].ShouldBe(ImageDecodeGuardConfiguration.DefaultMaxSourceHeight);
        exception.Data["MaxPixels"].ShouldBe(ImageDecodeGuardConfiguration.DefaultMaxSourcePixels);
        (await GetStoredBytesAsync(containerName, "bomb.png")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Reject_An_Image_With_Too_Many_Pixels_Per_Stored_Byte()
    {
        const string containerName = BlobStoringImagingTestModule.ResizeContainer;
        var content = TestImages.Solid(2000, 2000, SKEncodedImageFormat.Png);
        // Within the dimension and pixel limits; only the ratio is exceeded.
        (2000L * 2000 / content.Length).ShouldBeGreaterThan(ImageDecodeGuardConfiguration.DefaultMaxDecompressionRatio);

        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(containerName).SaveAsync("flat.png", content));

        exception.Code.ShouldBe(BlobStoringImagingErrorCodes.ImageTooLarge);
        exception.Data["MaxDecompressionRatio"].ShouldBe(ImageDecodeGuardConfiguration.DefaultMaxDecompressionRatio);
    }

    [Fact]
    public async Task Should_Apply_The_Configured_Limits()
    {
        const string containerName = BlobStoringImagingTestModule.GuardedContainer;

        var exception = await Should.ThrowAsync<BusinessException>(() => Container(containerName).SaveAsync(
            "wide.png",
            TestImages.Noise(BlobStoringImagingTestModule.GuardMaxSourceWidth + 1, 10, SKEncodedImageFormat.Png)));

        exception.Code.ShouldBe(BlobStoringImagingErrorCodes.ImageTooLarge);
        exception.Data["MaxWidth"].ShouldBe(BlobStoringImagingTestModule.GuardMaxSourceWidth);

        // At the limit it is accepted (and fits the resize box, so it is stored as it is).
        var content = TestImages.Noise(BlobStoringImagingTestModule.GuardMaxSourceWidth, 10, SKEncodedImageFormat.Png);
        await Container(containerName).SaveAsync("fits.png", content);
        (await GetStoredBytesAsync(containerName, "fits.png")).ShouldBe(content);
    }

    [Fact]
    public async Task Should_Guard_The_Compress_Contributor_Too()
    {
        const string containerName = BlobStoringImagingTestModule.CompressContainer;

        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(containerName).SaveAsync("bomb.jpg", TestHeaders.Jpeg(60_000, 60_000)));

        exception.Code.ShouldBe(BlobStoringImagingErrorCodes.ImageTooLarge);
    }

    [Fact]
    public void Should_Use_The_Defaults_When_Never_Configured()
    {
        var guard = new BlobContainerConfiguration().GetImageDecodeGuardConfiguration();

        guard.MaxSourceWidth.ShouldBe(4096);
        guard.MaxSourceHeight.ShouldBe(4096);
        guard.MaxSourcePixels.ShouldBe(16_000_000);
        guard.MaxDecompressionRatio.ShouldBe(100);
        guard.DecodeTimeout.ShouldBe(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ConfigureImageDecodeGuard_Should_Reject_An_Invalid_Configuration()
    {
        var configuration = new BlobContainerConfiguration();

        Should.Throw<AbpException>(() => configuration.ConfigureImageDecodeGuard(g => g.MaxSourceWidth = 0));
        Should.Throw<AbpException>(() => configuration.ConfigureImageDecodeGuard(g => g.MaxSourcePixels = -1));
        Should.Throw<AbpException>(() => configuration.ConfigureImageDecodeGuard(g => g.MaxDecompressionRatio = 0));
        Should.Throw<AbpException>(() => configuration.ConfigureImageDecodeGuard(g => g.DecodeTimeout = TimeSpan.Zero));
        Should.Throw<AbpException>(() => configuration.ConfigureImageDecodeGuard(g => g.DecodeTimeout = TimeSpan.FromDays(30)));
    }

    [Fact]
    public void ConfigureImageDecodeGuard_Should_Not_Add_A_Contributor()
    {
        var configuration = new BlobContainerConfiguration();

        configuration.ConfigureImageDecodeGuard(g => g.MaxSourcePixels = 1_000_000);

        configuration.PipelineContributors.ShouldBeEmpty();
        configuration.GetImageDecodeGuardConfiguration().MaxSourcePixels.ShouldBe(1_000_000);
    }
}
