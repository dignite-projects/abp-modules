using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// Stores the content GZip-compressed and decompresses it again while reading. Configure it with
/// <see cref="BlobContainerConfigurationExtensions.AddGZipContributor"/>.
/// <para>
/// While saving, the content is compressed eagerly into memory (the compressed stream has a known
/// <see cref="Stream.Length"/>), leaving the received stream open. The bytes read from the received stream are
/// counted and capped at the container's <see cref="MaxSizeContributor"/> limit or, without one,
/// <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/>: beyond it the save fails with
/// <see cref="BlobStoringPipelineErrorCodes.ContentTooLarge"/> (a seekable stream whose remaining length is
/// already larger is rejected without reading). While reading, the stored stream is wrapped
/// in a decompressing <see cref="GZipStream"/> that disposes it, so the stream returned by <c>GetAsync</c> is
/// read-only and not seekable. ABP's BLOB encryption, when enabled, always runs after the contributors, so the
/// content is compressed before it is encrypted.
/// </para>
/// <para>
/// <b>This contributor transforms the content, so it becomes part of the stored format:</b> a BLOB can only be
/// read with the same transforming contributors, in the same order, it was saved with. Adding it to (or removing
/// it from) a container that already has BLOBs makes the existing BLOBs fail to read. To change it, read the
/// BLOBs with the old configuration, then write them back with the new one.
/// </para>
/// </summary>
public class GZipContributor : IBlobPipelineContributor, ITransientDependency
{
    private const int CopyBufferSize = 81920;

    public virtual async Task OnSavingAsync(BlobPipelineContext context)
    {
        var configuration = context.Configuration.GetGZipContributorConfiguration();
        configuration.Validate();

        var maxBytes = BlobStreamBuffering.GetMaxBufferedBytes(context.Configuration);
        var input = context.BlobStream;
        if (input.CanSeek && input.Length - input.Position > maxBytes)
        {
            throw BlobStreamBuffering.CreateContentTooLargeException(maxBytes);
        }

        var compressedStream = new MemoryStream();
        var chunk = ArrayPool<byte>.Shared.Rent(CopyBufferSize);
        try
        {
            await using (var gzipStream = new GZipStream(compressedStream, configuration.CompressionLevel, leaveOpen: true))
            {
                long totalBytes = 0;
                int bytesRead;
                while ((bytesRead = await input.ReadAsync(chunk.AsMemory(0, CopyBufferSize), context.CancellationToken)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > maxBytes)
                    {
                        throw BlobStreamBuffering.CreateContentTooLargeException(maxBytes);
                    }

                    await gzipStream.WriteAsync(chunk.AsMemory(0, bytesRead), context.CancellationToken);
                }
            }
        }
        catch
        {
            // Only a stream assigned to context.BlobStream is disposed by the pipeline.
            await compressedStream.DisposeAsync();
            throw;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }

        compressedStream.Position = 0;
        context.BlobStream = compressedStream;
    }

    public virtual Task OnGettingAsync(BlobPipelineContext context)
    {
        // GZipStream disposes the stream it wraps (leaveOpen: false), as the pipeline requires while reading.
        context.BlobStream = new GZipStream(context.BlobStream, CompressionMode.Decompress);
        return Task.CompletedTask;
    }
}
