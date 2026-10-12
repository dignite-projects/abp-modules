using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SkiaSharp;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Imaging;
using Xunit;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The provider outcomes a real provider cannot be made to produce on demand: never being called, timing out,
/// throwing, and the Unsupported/Canceled results.
/// </summary>
public class FakeProvider_Tests : BlobStoringImagingTestBase<FakeImagingTestModule>
{
    private const string ResizeContainer = FakeImagingTestModule.ResizeContainer;
    private const string CompressContainer = FakeImagingTestModule.CompressContainer;

    // Wider than the fake-resize box, so the resizer is called.
    private static readonly byte[] Image = TestImages.Noise(FakeImagingTestModule.ResizeMaxWidth * 2, 50, SKEncodedImageFormat.Png);

    private FakeImageResizer Resizer => GetRequiredService<FakeImageResizer>();

    private FakeImageCompressor Compressor => GetRequiredService<FakeImageCompressor>();

    [Fact]
    public async Task Should_Reject_A_Huge_Canvas_Without_Calling_The_Provider()
    {
        // The fake throws if it is called; ImageTooLarge (not ImageProcessingFailed) proves it was not.
        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(ResizeContainer).SaveAsync("bomb.png", TestHeaders.Png(100_000, 100_000)));

        exception.Code.ShouldBe(BlobStoringImagingErrorCodes.ImageTooLarge);
        Resizer.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Should_Pass_The_Detected_Type_And_The_Box_To_The_Resizer()
    {
        Resizer.Operation = (stream, _, _) => Task.FromResult((stream, ImageProcessState.Done));

        await Container(ResizeContainer).SaveAsync("photo.png", Image);

        Resizer.CallCount.ShouldBe(1);
        Resizer.LastArgs.ShouldNotBeNull();
        Resizer.LastArgs.Width.ShouldBe((uint)FakeImagingTestModule.ResizeMaxWidth);
        Resizer.LastArgs.Height.ShouldBe(0u);
        Resizer.LastArgs.Mode.ShouldBe(ImageResizeMode.Max);
        // Done with the same instance: the content is stored as it is, rewound.
        (await GetStoredBytesAsync(ResizeContainer, "photo.png")).ShouldBe(Image);
    }

    [Fact]
    public async Task Should_Fail_With_ImageDecodeTimeout_When_The_Provider_Exceeds_The_Timeout()
    {
        Resizer.Operation = async (stream, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return (stream, ImageProcessState.Done);
        };

        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(ResizeContainer).SaveAsync("slow.png", Image));

        exception.Code.ShouldBe(BlobStoringImagingErrorCodes.ImageDecodeTimeout);
        exception.Data["DecodeTimeoutSeconds"].ShouldBe(FakeImagingTestModule.DecodeTimeout.TotalSeconds);
        (await GetStoredBytesAsync(ResizeContainer, "slow.png")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Rethrow_A_Cancellation_Requested_By_The_Caller()
    {
        using var cancellation = new CancellationTokenSource();
        Resizer.Operation = async (stream, _, cancellationToken) =>
        {
            await cancellation.CancelAsync();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return (stream, ImageProcessState.Done);
        };

        var exception = await Record.ExceptionAsync(
            () => Container(ResizeContainer).SaveAsync("cancelled.png", Image, cancellationToken: cancellation.Token));

        exception.ShouldBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task Should_Keep_The_Content_When_The_Provider_Does_Not_Support_It()
    {
        var unused = new TrackingStream([1, 2, 3], canSeek: true);
        Resizer.Operation = (_, _, _) => Task.FromResult<(Stream, ImageProcessState)>((unused, ImageProcessState.Unsupported));

        await Container(ResizeContainer).SaveAsync("unsupported.png", Image);

        (await GetStoredBytesAsync(ResizeContainer, "unsupported.png")).ShouldBe(Image);
        unused.IsDisposed.ShouldBeTrue(); // a stream the provider created but the pipeline does not own
    }

    [Fact]
    public async Task Should_Fail_A_Resize_The_Provider_Canceled()
    {
        Resizer.Operation = (stream, _, _) => Task.FromResult((stream, ImageProcessState.Canceled));

        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(ResizeContainer).SaveAsync("canceled.png", Image));

        exception.Code.ShouldBe(BlobStoringImagingErrorCodes.ImageProcessingFailed);
        (await GetStoredBytesAsync(ResizeContainer, "canceled.png")).ShouldBeNull();
    }

    [Fact]
    public async Task Should_Keep_The_Original_When_The_Compressor_Canceled()
    {
        Compressor.Operation = (stream, _, _) => Task.FromResult((stream, ImageProcessState.Canceled));

        await Container(CompressContainer).SaveAsync("canceled.png", Image);

        (await GetStoredBytesAsync(CompressContainer, "canceled.png")).ShouldBe(Image);
    }

    [Fact]
    public async Task Should_Fail_With_ImageProcessingFailed_When_The_Provider_Throws()
    {
        var failure = new InvalidDataException("corrupt image data");
        Compressor.Operation = (_, _, _) => throw failure;

        var exception = await Should.ThrowAsync<BusinessException>(
            () => Container(CompressContainer).SaveAsync("corrupt.png", Image));

        exception.Code.ShouldBe(BlobStoringImagingErrorCodes.ImageProcessingFailed);
        exception.InnerException.ShouldBeSameAs(failure);
    }

    [Fact]
    public async Task Should_Store_The_Stream_The_Provider_Returned()
    {
        var replacement = TestImages.Noise(10, 10, SKEncodedImageFormat.Png);
        Compressor.Operation = (_, mimeType, _) =>
        {
            mimeType.ShouldBe("image/png");
            return Task.FromResult<(Stream, ImageProcessState)>((new MemoryStream(replacement) { Position = 7 }, ImageProcessState.Done));
        };

        await Container(CompressContainer).SaveAsync("replaced.png", Image);

        // Rewound before it is stored.
        (await GetStoredBytesAsync(CompressContainer, "replaced.png")).ShouldBe(replacement);
    }
}
