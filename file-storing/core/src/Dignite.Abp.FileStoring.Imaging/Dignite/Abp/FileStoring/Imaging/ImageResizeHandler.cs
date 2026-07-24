using System;
using System.Threading;
using System.Threading.Tasks;
using Dignite.Abp.FileStoring;
using SixLabors.ImageSharp;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Imaging;

namespace Dignite.Abp.FileStoring.Imaging;

public class ImageResizeHandler : IFileHandler, ITransientDependency
{
    private readonly IImageResizer _imageResizer;
    private readonly IImageCompressor _imageCompressor;

    public ImageResizeHandler(IImageResizer imageResizer, IImageCompressor imageCompressor)
    {
        _imageResizer = imageResizer;
        _imageCompressor = imageCompressor;
    }

    public async Task ExecuteAsync(FileHandlerContext context)
    {
        var configuration = context.ContainerConfiguration.GetImageResizeConfiguration();

        if (!context.BlobStream.CanSeek)
        {
            throw new BusinessException(
                code: FileStoringImagingErrorCodes.ImageResizeFailure,
                message: "Image stream must support seeking."
            );
        }

        using var decodeTimeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(configuration.DecodeTimeoutSeconds));

        try
        {
            context.BlobStream.Position = 0;
            var imageInfo = await Image.IdentifyAsync(context.BlobStream, decodeTimeout.Token);
            var detectedFormat = imageInfo?.Metadata.DecodedImageFormat;
            if (detectedFormat == null ||
                !ImageFormatHelper.IsValidImage(detectedFormat.DefaultMimeType, ImageFormatHelper.AllowedImageUploadFormats))
            {
                if (!ImageFormatHelper.IsValidImage(context.MimeType, ImageFormatHelper.AllowedImageUploadFormats))
                {
                    return;
                }

                throw new BusinessException(
                    code: FileStoringImagingErrorCodes.ImageFormatNotSupported,
                    message: "The image format is not supported."
                );
            }

            var detectedImageInfo = imageInfo!;
            var totalPixels = (long)detectedImageInfo.Width * detectedImageInfo.Height;
            if (detectedImageInfo.Width > configuration.MaxImageWidth ||
                detectedImageInfo.Height > configuration.MaxImageHeight ||
                totalPixels > configuration.MaxPixelCount ||
                context.BlobStream.Length > 0 &&
                totalPixels / (double)context.BlobStream.Length > configuration.MaxDecompressionRatio)
            {
                throw new BusinessException(
                    code: FileStoringImagingErrorCodes.ImageTooLarge,
                    message: "The image is too large to process safely.",
                    details: $"Maximum dimensions: {configuration.MaxImageWidth}x{configuration.MaxImageHeight}; maximum pixels: {configuration.MaxPixelCount}."
                );
            }

            context.BlobStream.Position = 0;
            using (var image = await Image.LoadAsync(context.BlobStream, decodeTimeout.Token))
            {
                context.BlobStream.Position = 0;
                if (configuration.ImageSizeMustBeLargerThanPreset &&
                    (image.Width < configuration.ImageWidth || image.Height < configuration.ImageHeight))
                {
                    throw new BusinessException(
                        code: FileStoringImagingErrorCodes.ImageSizeTooSmall,
                        message: "Image size must be larger than preset!",
                        details: "Uploaded image must be larger than: " + configuration.ImageWidth + "x" + configuration.ImageHeight
                    );
                }

                if (image.Width > configuration.ImageWidth || image.Height > configuration.ImageHeight)
                {
                    var resizeResult = await _imageResizer.ResizeAsync(
                        context.BlobStream,
                        new ImageResizeArgs((uint)configuration.ImageWidth, (uint)configuration.ImageHeight, ImageResizeMode.Max),
                        detectedFormat.DefaultMimeType
                    );

                    if (resizeResult.State == ImageProcessState.Done)
                    {
                        context.BlobStream = resizeResult.Result;
                    }
                    else if (resizeResult.Result is not null && context.BlobStream != resizeResult.Result && resizeResult.Result.CanRead)
                    {
                        context.BlobStream = resizeResult.Result;
                    }
                    else
                    {
                        throw new BusinessException(
                            code: FileStoringImagingErrorCodes.ImageResizeFailure,
                            message: resizeResult.State.ToString()
                        );
                    }
                }

                var compressResult = await _imageCompressor.CompressAsync(
                    context.BlobStream,
                    mimeType: detectedFormat.DefaultMimeType
                );

                if (compressResult.State == ImageProcessState.Done)
                {
                    context.BlobStream = compressResult.Result;
                }
                else if (compressResult.Result is not null && context.BlobStream != compressResult.Result && compressResult.Result.CanRead)
                {
                    context.BlobStream = compressResult.Result;
                }
            }
        }
        catch (OperationCanceledException) when (decodeTimeout.IsCancellationRequested)
        {
            throw new BusinessException(
                code: FileStoringImagingErrorCodes.ImageDecodeTimeout,
                message: "Image decoding timed out."
            );
        }
        catch (ImageFormatException) when (!ImageFormatHelper.IsValidImage(context.MimeType, ImageFormatHelper.AllowedImageUploadFormats))
        {
            return;
        }
        catch (ImageFormatException exception)
        {
            throw new BusinessException(
                code: FileStoringImagingErrorCodes.ImageFormatNotSupported,
                message: "The image content is invalid.",
                details: exception.Message
            );
        }
    }
}
