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
/// <see cref="Stream.Length"/>), leaving the received stream open. While reading, the stored stream is wrapped
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
    public virtual async Task OnSavingAsync(BlobPipelineContext context)
    {
        var configuration = context.Configuration.GetGZipContributorConfiguration();
        configuration.Validate();

        var compressedStream = new MemoryStream();
        try
        {
            await using (var gzipStream = new GZipStream(compressedStream, configuration.CompressionLevel, leaveOpen: true))
            {
                await context.BlobStream.CopyToAsync(gzipStream, context.CancellationToken);
            }
        }
        catch
        {
            // Only a stream assigned to context.BlobStream is disposed by the pipeline.
            await compressedStream.DisposeAsync();
            throw;
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
