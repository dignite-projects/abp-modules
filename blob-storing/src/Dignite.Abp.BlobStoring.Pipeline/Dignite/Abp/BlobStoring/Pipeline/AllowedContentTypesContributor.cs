using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobStoring.Pipeline;

/// <summary>
/// Rejects content whose type, detected from the bytes by <see cref="IMimeTypeDetector"/> (and reconciled
/// with the extension of the BLOB name), is not in
/// <see cref="AllowedContentTypesContributorConfiguration.AllowedContentTypes"/>, with
/// <see cref="BlobStoringPipelineErrorCodes.ContentTypeNotAllowed"/>; content that contradicts its
/// extension fails with <see cref="BlobStoringPipelineErrorCodes.ContentTypeMismatch"/>. Configure it with
/// <see cref="BlobContainerConfigurationExtensions.AddAllowedContentTypesContributor"/>.
/// <para>
/// Detection needs to re-read the content. A stream that is already seekable and at its start (for example
/// after <see cref="MaxSizeContributor"/>) is probed in place and rewound; any other stream is first
/// materialized into memory, and that copy replaces <see cref="BlobPipelineContext.BlobStream"/>, as the
/// pipeline contract requires of a contributor that consumes the content. Either way the content is capped at
/// the container's <see cref="MaxSizeContributor"/> limit or, without one,
/// <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/>, beyond which it fails with
/// <see cref="BlobStoringPipelineErrorCodes.ContentTooLarge"/>.
/// </para>
/// <para>
/// It does not change the content, so it can be added to or removed from a container that already has BLOBs.
/// Run it before any transforming contributor (such as <see cref="GZipContributor"/>), or it inspects the
/// transformed bytes.
/// </para>
/// </summary>
public class AllowedContentTypesContributor : IBlobPipelineContributor, ITransientDependency
{
    protected IMimeTypeDetector MimeTypeDetector { get; }

    public AllowedContentTypesContributor(IMimeTypeDetector mimeTypeDetector)
    {
        MimeTypeDetector = mimeTypeDetector;
    }

    public virtual async Task OnSavingAsync(BlobPipelineContext context)
    {
        var configuration = context.Configuration.GetAllowedContentTypesContributorConfiguration();
        configuration.Validate();

        var stream = await BlobStreamBuffering.EnsureSeekableAsync(context, GetMaxBufferedBytes(context.Configuration));

        var contentType = await MimeTypeDetector.DetectAsync(stream, context.BlobName, context.CancellationToken);

        if (!IsAllowed(contentType, configuration.AllowedContentTypes, configuration.AllowUnidentified))
        {
            throw CreateNotAllowedException(contentType);
        }
    }

    public virtual Task OnGettingAsync(BlobPipelineContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// The cap for the content this contributor reads (a seekable stream longer than it is rejected, any other
    /// is buffered up to it): the container's <see cref="MaxSizeContributor"/> limit, or
    /// <see cref="BlobStoringPipelineConsts.DefaultMaxBufferedBytes"/> when it has none.
    /// </summary>
    protected virtual long GetMaxBufferedBytes(BlobContainerConfiguration configuration)
    {
        return BlobStreamBuffering.GetMaxBufferedBytes(configuration);
    }

    /// <summary>
    /// Whether <paramref name="contentType"/> matches an entry of <paramref name="allowedContentTypes"/>
    /// (case-insensitively; <c>type/*</c> matches every subtype). Unidentified content
    /// (<see cref="Pipeline.MimeTypeDetector.DefaultMimeType"/>) only passes with
    /// <paramref name="allowUnidentified"/> or an exact <c>application/octet-stream</c> entry.
    /// </summary>
    protected virtual bool IsAllowed(string contentType, IReadOnlyCollection<string> allowedContentTypes, bool allowUnidentified)
    {
        if (string.Equals(contentType, Pipeline.MimeTypeDetector.DefaultMimeType, StringComparison.OrdinalIgnoreCase))
        {
            if (allowUnidentified)
            {
                return true;
            }

            foreach (var allowed in allowedContentTypes)
            {
                if (string.Equals(allowed, contentType, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var allowed in allowedContentTypes)
        {
            if (allowed.EndsWith("/*", StringComparison.Ordinal))
            {
                // "image/*" -> "image/"
                if (contentType.StartsWith(allowed[..^1], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else if (string.Equals(allowed, contentType, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    protected virtual Exception CreateNotAllowedException(string contentType)
    {
        return new BusinessException(
                code: BlobStoringPipelineErrorCodes.ContentTypeNotAllowed,
                message: $"The content type '{contentType}' is not allowed.")
            .WithData("ContentType", contentType);
    }
}
