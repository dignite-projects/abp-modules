using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.ExceptionHandling;
using Volo.Abp.Imaging;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// The save-side flow shared by the image contributors. While saving:
/// <list type="number">
/// <item>The content is made seekable with <see cref="BlobStreamBuffering.EnsureSeekableAsync"/>, capped at the
/// container's <c>MaxSizeContributor</c> limit or, without one, <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/>.</item>
/// <item>Its type is detected from the bytes with <see cref="IMimeTypeDetector"/>. Content that is not
/// <c>image/*</c> passes through untouched (a container may hold mixed content; use
/// <c>AddAllowedContentTypesContributor</c> to forbid anything but images).</item>
/// <item>The decode guard (<see cref="ImageDecodeGuardConfiguration"/>) checks the dimensions read from the image
/// header before anything decodes the image, failing with <see cref="BlobStoringImagingErrorCodes.ImageTooLarge"/>.</item>
/// <item>The operation (<see cref="ProcessAsync"/>) runs under the guard's decode timeout, failing with
/// <see cref="BlobStoringImagingErrorCodes.ImageDecodeTimeout"/> when the timeout (not the caller) cancels it, and
/// with <see cref="BlobStoringImagingErrorCodes.ImageProcessingFailed"/> when the provider throws.</item>
/// <item>The result: <see cref="ImageProcessState.Done"/> replaces the content;
/// <see cref="ImageProcessState.Unsupported"/> (the provider does not handle this format) keeps it;
/// <see cref="ImageProcessState.Canceled"/> goes to <see cref="HandleCanceledResult"/>.</item>
/// </list>
/// The received stream is never disposed, and a replacement is assigned to <see cref="BlobPipelineContext.BlobStream"/>
/// so the pipeline disposes it.
/// <para>
/// Both contributors are one-way transforms: the stored BLOB is an ordinary image, and nothing is undone while
/// reading. So, unlike GZip, they can be added to (or removed from) a container that already has BLOBs; BLOBs saved
/// before keep reading as they were stored.
/// </para>
/// </summary>
public abstract class ImageContributorBase : IBlobPipelineContributor
{
    protected IMimeTypeDetector MimeTypeDetector { get; }

    protected ImageContributorBase(IMimeTypeDetector mimeTypeDetector)
    {
        MimeTypeDetector = mimeTypeDetector;
    }

    public virtual async Task OnSavingAsync(BlobPipelineContext context)
    {
        ValidateConfiguration(context.Configuration);
        var guard = context.Configuration.GetImageDecodeGuardConfiguration();
        guard.Validate();

        var stream = await BlobStreamBuffering.EnsureSeekableAsync(context, GetMaxBufferedBytes(context.Configuration));

        var mimeType = await MimeTypeDetector.DetectAsync(stream, context.BlobName, context.CancellationToken);
        if (!IsImage(mimeType))
        {
            return;
        }

        var dimensions = ReadDimensions(stream);
        CheckDecodeGuard(dimensions, stream.Length, guard);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
        timeoutSource.CancelAfter(guard.DecodeTimeout);

        ImageProcessResult<Stream>? result;
        try
        {
            stream.Position = 0;
            result = await ProcessAsync(context, stream, mimeType, dimensions, timeoutSource.Token);
        }
        catch (OperationCanceledException) when (IsTimeout(context, timeoutSource))
        {
            throw CreateDecodeTimeoutException(guard.DecodeTimeout);
        }
        catch (Exception exception) when (IsProviderFailure(exception))
        {
            throw CreateProcessingFailedException(exception);
        }

        if (result == null)
        {
            // Nothing to do for this image.
            stream.Position = 0;
            return;
        }

        switch (result.State)
        {
            case ImageProcessState.Done:
                if (ReferenceEquals(result.Result, stream))
                {
                    stream.Position = 0;
                }
                else
                {
                    if (result.Result.CanSeek)
                    {
                        result.Result.Position = 0;
                    }

                    context.BlobStream = result.Result;
                }

                break;

            case ImageProcessState.Unsupported:
                await DisposeIfNotAsync(result.Result, stream);
                stream.Position = 0;
                break;

            case ImageProcessState.Canceled:
                await DisposeIfNotAsync(result.Result, stream);
                context.CancellationToken.ThrowIfCancellationRequested();
                if (IsTimeout(context, timeoutSource))
                {
                    throw CreateDecodeTimeoutException(guard.DecodeTimeout);
                }

                stream.Position = 0;
                HandleCanceledResult(context, stream);
                break;

            default:
                await DisposeIfNotAsync(result.Result, stream);
                throw CreateProcessingFailedException(null);
        }
    }

    /// <summary>
    /// Does nothing: the save-side transformation is one-way, and the stored BLOB is read as it is.
    /// </summary>
    public virtual Task OnGettingAsync(BlobPipelineContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates the contributor's own configuration, throwing <see cref="AbpException"/> when it cannot be applied.
    /// </summary>
    protected abstract void ValidateConfiguration(BlobContainerConfiguration configuration);

    /// <summary>
    /// Runs the operation on <paramref name="stream"/> (seekable, at position 0, an image of
    /// <paramref name="mimeType"/>), passing <paramref name="cancellationToken"/> (the operation's token linked with
    /// the decode timeout) to the provider. <paramref name="dimensions"/> are the dimensions read from the header, or
    /// <c>null</c> when they could not be read. Returns <c>null</c> when there is nothing to do; must not dispose
    /// <paramref name="stream"/>.
    /// </summary>
    protected abstract Task<ImageProcessResult<Stream>?> ProcessAsync(
        BlobPipelineContext context,
        Stream stream,
        string mimeType,
        (int Width, int Height)? dimensions,
        CancellationToken cancellationToken);

    /// <summary>
    /// Called when the provider returns <see cref="ImageProcessState.Canceled"/> while the operation was not cancelled
    /// and did not time out; <paramref name="stream"/> is the unchanged content, rewound. By default it fails with
    /// <see cref="BlobStoringImagingErrorCodes.ImageProcessingFailed"/>.
    /// </summary>
    protected virtual void HandleCanceledResult(BlobPipelineContext context, Stream stream)
    {
        throw CreateProcessingFailedException(null);
    }

    /// <summary>
    /// The cap for buffering a non-seekable stream: the container's <c>MaxSizeContributor</c> limit, or
    /// <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/> when it has none.
    /// </summary>
    protected virtual long GetMaxBufferedBytes(BlobContainerConfiguration configuration)
    {
        var maxSize = configuration.GetMaxSizeContributorConfiguration().MaxSizeInBytes;
        return maxSize > 0 ? maxSize : BlobStoringPipelineConsts.DefaultMaxBufferedBytes;
    }

    /// <summary>
    /// Whether the detected content type is an image this contributor hands to the provider: any <c>image/*</c>.
    /// </summary>
    protected virtual bool IsImage(string mimeType)
    {
        return mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The dimensions declared by the image header, read without decoding; <c>null</c> when they cannot be read.
    /// <paramref name="stream"/> is seekable and its position is restored.
    /// </summary>
    protected virtual (int Width, int Height)? ReadDimensions(Stream stream)
    {
        return ImageHeaderReader.Read(stream);
    }

    /// <summary>
    /// Fails with <see cref="BlobStoringImagingErrorCodes.ImageTooLarge"/> when the image is wider, taller or larger
    /// than the guard allows, or declares more pixels per stored byte than
    /// <see cref="ImageDecodeGuardConfiguration.MaxDecompressionRatio"/>. Skipped when the dimensions are unknown.
    /// </summary>
    protected virtual void CheckDecodeGuard(
        (int Width, int Height)? dimensions,
        long storedLength,
        ImageDecodeGuardConfiguration guard)
    {
        if (dimensions == null)
        {
            return;
        }

        var (width, height) = dimensions.Value;
        var pixels = (long)width * height;
        if (width > guard.MaxSourceWidth ||
            height > guard.MaxSourceHeight ||
            pixels > guard.MaxSourcePixels ||
            storedLength > 0 && pixels / (double)storedLength > guard.MaxDecompressionRatio)
        {
            throw new BusinessException(
                    code: BlobStoringImagingErrorCodes.ImageTooLarge,
                    message: "The image is too large to process safely.",
                    details: $"The image is {width}x{height} ({pixels} pixels in {storedLength} bytes). Maximum " +
                             $"dimensions: {guard.MaxSourceWidth}x{guard.MaxSourceHeight}; maximum pixels: " +
                             $"{guard.MaxSourcePixels}; maximum pixels per byte: {guard.MaxDecompressionRatio}.")
                .WithData("Width", width)
                .WithData("Height", height)
                .WithData("MaxWidth", guard.MaxSourceWidth)
                .WithData("MaxHeight", guard.MaxSourceHeight)
                .WithData("MaxPixels", guard.MaxSourcePixels)
                .WithData("MaxDecompressionRatio", guard.MaxDecompressionRatio);
        }
    }

    /// <summary>
    /// Whether an exception thrown by <see cref="ProcessAsync"/> is the provider failing on the content (mapped to
    /// <see cref="BlobStoringImagingErrorCodes.ImageProcessingFailed"/>), rather than a cancellation, an out-of-memory
    /// condition or an exception that already carries an error code.
    /// </summary>
    protected virtual bool IsProviderFailure(Exception exception)
    {
        return exception is not (OperationCanceledException or OutOfMemoryException or IBusinessException or IHasErrorCode);
    }

    protected virtual Exception CreateDecodeTimeoutException(TimeSpan decodeTimeout)
    {
        return new BusinessException(
                code: BlobStoringImagingErrorCodes.ImageDecodeTimeout,
                message: $"Processing the image took longer than {decodeTimeout.TotalSeconds} seconds.")
            .WithData("DecodeTimeoutSeconds", decodeTimeout.TotalSeconds);
    }

    protected virtual Exception CreateProcessingFailedException(Exception? innerException)
    {
        return new BusinessException(
            code: BlobStoringImagingErrorCodes.ImageProcessingFailed,
            message: "The image could not be processed.",
            details: innerException?.Message,
            innerException: innerException);
    }

    private static bool IsTimeout(BlobPipelineContext context, CancellationTokenSource timeoutSource)
    {
        return timeoutSource.IsCancellationRequested && !context.CancellationToken.IsCancellationRequested;
    }

    private static async Task DisposeIfNotAsync(Stream candidate, Stream keep)
    {
        // A stream the provider created but the pipeline will not own.
        if (!ReferenceEquals(candidate, keep))
        {
            await candidate.DisposeAsync();
        }
    }
}
