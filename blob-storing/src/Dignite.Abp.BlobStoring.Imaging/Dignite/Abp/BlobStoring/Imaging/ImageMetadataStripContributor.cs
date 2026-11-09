using System;
using System.IO;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Pipeline;
using Volo.Abp.BlobStoring;
using Volo.Abp.DependencyInjection;

namespace Dignite.Abp.BlobStoring.Imaging;

/// <summary>
/// Removes the metadata that editing software piles into a JPEG or PNG and that nothing displaying it reads, keeping
/// every byte of the image. Configure it with
/// <see cref="ImagingBlobContainerConfigurationExtensions.AddImageMetadataStripContributor"/>.
/// <para>
/// <b>What goes.</b> From a JPEG: Extended XMP (APP1 <c>http://ns.adobe.com/xmp/extension/</c>), the Photoshop image
/// resources (APP13) and comments. From a PNG: the text chunks (<c>tEXt</c>, <c>zTXt</c>, <c>iTXt</c>) and
/// <c>tIME</c>. Extended XMP is where Photoshop puts what does not fit in one XMP segment, typically
/// <c>photoshop:DocumentAncestors</c>: an 18 MB banner whose image was 0.9 MB was nearly all of it.
/// </para>
/// <para>
/// <b>What stays.</b> Everything else, byte for byte: the image data, the main XMP packet, EXIF (the orientation, and
/// any location a camera recorded), the ICC profile, the Adobe segment, and whatever follows the image. It is not a
/// privacy filter and not a sanitizer.
/// </para>
/// <para>
/// No image library and no decoding: the segments or chunks are walked in managed code, so the
/// <see cref="ImageDecodeGuardConfiguration"/> does not apply. Another format, content whose structure does not
/// parse, and an image with nothing to remove pass through unchanged. While reading it does nothing: the stored
/// BLOB is an ordinary image, so the contributor can be added to a container that already has BLOBs.
/// </para>
/// </summary>
public class ImageMetadataStripContributor : IBlobPipelineContributor, ITransientDependency
{
    public virtual async Task OnSavingAsync(BlobPipelineContext context)
    {
        context.Configuration.GetImageMetadataStripContributorConfiguration().Validate();

        var maxBytes = BlobStreamBuffering.GetMaxBufferedBytes(context.Configuration);
        var stream = await BlobStreamBuffering.EnsureSeekableAsync(context, maxBytes);

        byte[]? stripped;
        if (stream is MemoryStream memory && memory.TryGetBuffer(out var buffer))
        {
            stripped = Strip(buffer.AsSpan(0, (int)memory.Length));
        }
        else
        {
            stream.Position = 0;
            await using var copy = await BlobStreamBuffering.CopyToBufferAsync(stream, maxBytes, context.CancellationToken);
            stripped = Strip(copy.GetBuffer().AsSpan(0, (int)copy.Length));
        }

        stream.Position = 0;
        if (stripped != null)
        {
            context.BlobStream = new MemoryStream(stripped, writable: false);
        }
    }

    public virtual Task OnGettingAsync(BlobPipelineContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// <paramref name="content"/> without its removable metadata, or <c>null</c> to keep the content as it is.
    /// </summary>
    protected virtual byte[]? Strip(ReadOnlySpan<byte> content)
    {
        return ImageMetadataStripper.Strip(content);
    }
}
