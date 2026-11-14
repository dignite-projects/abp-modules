using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.BlobStoring;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// Capped buffering for <see cref="IBlobPipelineContributor"/>s that need to re-read the content: the contributors
/// of this package, and contributors in other packages (such as <c>Dignite.Abp.BlobStoring.Imaging</c>) that inspect
/// or transform it. Every copy is bounded, so content is never buffered without a limit, and a copy that becomes the
/// content is assigned to <see cref="BlobPipelineContext.BlobStream"/>, so the pipeline owns and disposes it as the
/// <see cref="IBlobPipelineContributor"/> contract requires.
/// <para>
/// A copy the pipeline makes (<see cref="EnsureSeekableAsync"/>, <c>MaxSizeContributor</c>) is
/// <see cref="CopyToSeekableAsync"/>'s: in memory up to <see cref="BlobStoringPipelineConsts.MemoryBufferThreshold"/>, in a
/// temporary file beyond it, so a large upload does not hold its whole size in memory.
/// </para>
/// </summary>
public static class BlobStreamBuffering
{
    private const int CopyBufferSize = 81920;

    /// <summary>
    /// The cap every content-reading contributor enforces for a container: its <c>MaxSizeContributor</c> limit
    /// when one is configured (<c>&gt; 0</c>), else <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/>.
    /// </summary>
    public static long GetMaxBufferedBytes(BlobContainerConfiguration configuration)
    {
        var maxSize = configuration.GetMaxSizeContributorConfiguration().MaxSizeInBytes;
        return maxSize > 0 ? maxSize : BlobStoringPipelineConsts.DefaultMaxBufferedBytes;
    }

    /// <summary>
    /// Returns <see cref="BlobPipelineContext.BlobStream"/> when it is already seekable, positioned at its
    /// start and not longer than <paramref name="maxBytes"/> (a longer one fails with
    /// <see cref="BlobStoringPipelineErrorCodes.ContentTooLarge"/> before anything is read); otherwise copies it
    /// from its current position with <see cref="CopyToSeekableAsync"/> (failing beyond <paramref name="maxBytes"/>) and
    /// assigns the copy to <see cref="BlobPipelineContext.BlobStream"/>, which makes the pipeline own and dispose it after
    /// the save.
    /// </summary>
    public static async Task<Stream> EnsureSeekableAsync(BlobPipelineContext context, long maxBytes)
    {
        var stream = context.BlobStream;
        if (stream.CanSeek && stream.Position == 0)
        {
            if (stream.Length > maxBytes)
            {
                throw CreateContentTooLargeException(maxBytes);
            }

            return stream;
        }

        var buffer = await CopyToSeekableAsync(stream, maxBytes, context.CancellationToken);
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

    /// <summary>
    /// Copies <paramref name="source"/> from its current position into a seekable buffer: memory while the content is
    /// no longer than <see cref="BlobStoringPipelineConsts.MemoryBufferThreshold"/>, a temporary file once it is - the
    /// bytes read so far move there and the rest follows. The cap is <see cref="CopyToBufferAsync"/>'s: it fails with
    /// <see cref="BlobStoringPipelineErrorCodes.ContentTooLarge"/> as soon as more than <paramref name="maxBytes"/> bytes
    /// have been read, and before reading anything when a seekable source already reports a larger remaining length.
    /// <para>
    /// The temporary file is in <see cref="Path.GetTempPath"/> and is deleted when the buffer is disposed
    /// (<see cref="FileOptions.DeleteOnClose"/>), so whoever owns the buffer - the pipeline, once it is assigned to
    /// <see cref="BlobPipelineContext.BlobStream"/> - must dispose it. <paramref name="source"/> is left open; the buffer
    /// is disposed on failure and returned positioned at 0 on success.
    /// </para>
    /// </summary>
    public static async Task<Stream> CopyToSeekableAsync(Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        var knownLength = source.CanSeek ? Math.Max(0, source.Length - source.Position) : 0;
        if (knownLength > maxBytes)
        {
            throw CreateContentTooLargeException(maxBytes);
        }

        var threshold = BlobStoringPipelineConsts.MemoryBufferThreshold;
        Stream buffer = knownLength > threshold
            ? CreateTemporaryFile()
            : new MemoryStream((int)Math.Min(knownLength, Array.MaxLength));
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

                if (buffer is MemoryStream memory && totalBytes > threshold)
                {
                    buffer = await MoveToTemporaryFileAsync(memory, cancellationToken);
                }

                await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
            }

            await buffer.FlushAsync(cancellationToken);
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

    /// <summary>
    /// A temporary file holding what <paramref name="memory"/> holds, positioned at its end; <paramref name="memory"/>
    /// is disposed. A file that cannot be written is disposed - and so deleted - before the exception goes on.
    /// </summary>
    private static async Task<Stream> MoveToTemporaryFileAsync(MemoryStream memory, CancellationToken cancellationToken)
    {
        var file = CreateTemporaryFile();
        try
        {
            memory.Position = 0;
            await memory.CopyToAsync(file, CopyBufferSize, cancellationToken);
        }
        catch
        {
            await file.DisposeAsync();
            throw;
        }

        await memory.DisposeAsync();
        return file;
    }

    private static FileStream CreateTemporaryFile()
    {
        return new FileStream(
            Path.Combine(Path.GetTempPath(), "dignite-blob-" + Guid.NewGuid().ToString("N") + ".tmp"),
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None,
            CopyBufferSize,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
    }

    /// <summary>
    /// The <see cref="BlobStoringPipelineErrorCodes.ContentTooLarge"/> exception for a limit of
    /// <paramref name="maxBytes"/>, with the limit as <c>MaxSizeInBytes</c> data.
    /// </summary>
    public static BusinessException CreateContentTooLargeException(long maxBytes)
    {
        return new BusinessException(
                code: BlobStoringPipelineErrorCodes.ContentTooLarge,
                message: $"The content is too large. It cannot exceed {maxBytes} bytes.")
            .WithData("MaxSizeInBytes", maxBytes);
    }
}
