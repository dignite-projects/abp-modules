using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// Rejects content larger than <see cref="MaxSizeContributorConfiguration.MaxSizeInBytes"/> with
/// <see cref="BlobStoringPipelineErrorCodes.ContentTooLarge"/>. Configure it with
/// <see cref="BlobContainerConfigurationExtensions.AddMaxSizeContributor"/>.
/// <para>
/// The limit binds before or while the content is read: a seekable stream whose remaining length already exceeds it
/// is rejected without reading; any other stream is copied chunk by chunk - into memory, then a temporary file past
/// <see cref="BlobStoringPipelineConsts.MemoryBufferThreshold"/> - and rejected as soon as the copy passes the limit.
/// Nothing beyond the limit is ever buffered, and nothing is saved when it throws.
/// </para>
/// <para>
/// What leaves this contributor is a seekable, length-aware stream, deliberately: contributors that run after this one
/// and need to re-read the content (content sniffing, image decoding) use it without buffering again, and storage
/// providers that need the object size before uploading get a known <see cref="Stream.Length"/>. A stream that is
/// already seekable and at its start is that stream, and is kept as it is; any other is replaced by the copy.
/// Configure this contributor first.
/// </para>
/// <para>
/// It does not change the content, so it can be added to or removed from a container that already has BLOBs.
/// </para>
/// </summary>
public class MaxSizeContributor : IBlobPipelineContributor, ITransientDependency
{
    public virtual async Task OnSavingAsync(BlobPipelineContext context)
    {
        var configuration = context.Configuration.GetMaxSizeContributorConfiguration();
        configuration.Validate();

        var stream = context.BlobStream;
        if (stream.CanSeek && stream.Position == 0)
        {
            // Already what a copy would give the contributors after this one: nothing to buffer.
            if (stream.Length > configuration.MaxSizeInBytes)
            {
                throw BlobStreamBuffering.CreateContentTooLargeException(configuration.MaxSizeInBytes);
            }

            return;
        }

        context.BlobStream = await CopyToBufferAsync(
            stream,
            configuration.MaxSizeInBytes,
            context.CancellationToken);
    }

    public virtual Task OnGettingAsync(BlobPipelineContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Copies <paramref name="source"/> into a seekable buffer - memory, or a temporary file past
    /// <see cref="BlobStoringPipelineConsts.MemoryBufferThreshold"/> (<see cref="BlobStreamBuffering.CopyToSeekableAsync"/>)
    /// - failing with <see cref="BlobStoringPipelineErrorCodes.ContentTooLarge"/> beyond <paramref name="maxSizeInBytes"/>.
    /// Leaves <paramref name="source"/> open, and disposes the buffer itself when the copy fails (it is not yet
    /// tracked by the pipeline at that point).
    /// </summary>
    protected virtual async Task<Stream> CopyToBufferAsync(
        Stream source,
        long maxSizeInBytes,
        CancellationToken cancellationToken)
    {
        return await BlobStreamBuffering.CopyToSeekableAsync(source, maxSizeInBytes, cancellationToken);
    }
}
