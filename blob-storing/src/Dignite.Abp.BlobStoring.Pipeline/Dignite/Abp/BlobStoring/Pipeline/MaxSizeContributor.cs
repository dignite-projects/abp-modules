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
/// The limit binds while the content is read: a seekable stream whose remaining length already exceeds it is
/// rejected without reading; any other stream is copied chunk by chunk into memory and rejected as soon as the
/// copy passes the limit. Nothing beyond the limit is ever buffered, and nothing is saved when it throws.
/// </para>
/// <para>
/// The copy then replaces <see cref="BlobPipelineContext.BlobStream"/>. That materialized, seekable,
/// length-aware stream is deliberate: contributors that run after this one and need to re-read the content
/// (content sniffing, image decoding) use it without buffering again, and storage providers that need the
/// object size before uploading get a known <see cref="Stream.Length"/>. Configure this contributor first.
/// Because the content is held in memory, limits above ~2 GB are not supported.
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

        context.BlobStream = await CopyToBufferAsync(
            context.BlobStream,
            configuration.MaxSizeInBytes,
            context.CancellationToken);
    }

    public virtual Task OnGettingAsync(BlobPipelineContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Copies <paramref name="source"/> into memory, failing with
    /// <see cref="BlobStoringPipelineErrorCodes.ContentTooLarge"/> beyond <paramref name="maxSizeInBytes"/>.
    /// Leaves <paramref name="source"/> open, and disposes the buffer itself when the copy fails (it is not yet
    /// tracked by the pipeline at that point).
    /// </summary>
    protected virtual async Task<Stream> CopyToBufferAsync(
        Stream source,
        long maxSizeInBytes,
        CancellationToken cancellationToken)
    {
        return await BlobStreamBuffering.CopyToBufferAsync(source, maxSizeInBytes, cancellationToken);
    }
}
