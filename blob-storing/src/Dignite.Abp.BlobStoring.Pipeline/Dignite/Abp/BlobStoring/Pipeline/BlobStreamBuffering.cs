using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// Capped buffering shared by the contributors that need a seekable, length-aware stream.
/// </summary>
internal static class BlobStreamBuffering
{
    private const int CopyBufferSize = 81920;

    /// <summary>
    /// Returns <see cref="BlobPipelineContext.BlobStream"/> when it is already seekable and positioned at
    /// its start; otherwise copies it from its current position into memory (failing beyond
    /// <paramref name="maxBytes"/>) and assigns the copy to <see cref="BlobPipelineContext.BlobStream"/>,
    /// which makes the pipeline own and dispose it after the save.
    /// </summary>
    public static async Task<Stream> EnsureSeekableAsync(BlobPipelineContext context, long maxBytes)
    {
        var stream = context.BlobStream;
        if (stream.CanSeek && stream.Position == 0)
        {
            return stream;
        }

        var buffer = await CopyToBufferAsync(stream, maxBytes, context.CancellationToken);
        context.BlobStream = buffer;
        return buffer;
    }

    /// <summary>
    /// Copies <paramref name="source"/> from its current position into memory, failing as soon as more
    /// than <paramref name="maxBytes"/> bytes have been read (and before reading anything when a seekable
    /// source already reports a larger remaining length). <paramref name="source"/> is left open; the
    /// buffer is disposed on failure and returned positioned at 0 on success.
    /// </summary>
    public static async Task<MemoryStream> CopyToBufferAsync(Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        var knownLength = source.CanSeek ? Math.Max(0, source.Length - source.Position) : 0;
        if (knownLength > maxBytes)
        {
            throw CreateContentTooLargeException(maxBytes);
        }

        var buffer = new MemoryStream((int)Math.Min(knownLength, Array.MaxLength));
        var chunk = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            long totalBytes = 0;
            int bytesRead;
            while ((bytesRead = await source.ReadAsync(chunk.AsMemory(0, CopyBufferSize), cancellationToken)) > 0)
            {
                totalBytes += bytesRead;
                if (totalBytes > maxBytes)
                {
                    throw CreateContentTooLargeException(maxBytes);
                }

                await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
            }
        }
        catch
        {
            await buffer.DisposeAsync();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        buffer.Position = 0;
        return buffer;
    }

    public static BusinessException CreateContentTooLargeException(long maxBytes)
    {
        return new BusinessException(
                code: BlobStoringPipelineErrorCodes.ContentTooLarge,
                message: $"The content is too large. It cannot exceed {maxBytes} bytes.")
            .WithData("MaxSizeInBytes", maxBytes);
    }
}
