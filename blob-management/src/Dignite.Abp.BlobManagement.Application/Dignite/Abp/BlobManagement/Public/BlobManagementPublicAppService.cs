using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Imaging;
using Volo.Abp.Uow;
using Volo.Abp.Validation;

namespace Dignite.Abp.BlobManagement.Public;

/// <summary>
/// Serves the files their business system marked public (<c>FileDescriptor.IsPublic</c>) to anyone, in the request's
/// tenant (an anonymous request names it with <c>?__tenant=</c>, see <see cref="BlobManagementPublicUrl"/>). Not gated by
/// <c>BlobManagement.Enable</c>: turning the Blob Management off stops a tenant storing, not its published files being
/// read.
/// <para>
/// A size is rendered on every request with ABP's <see cref="IImageResizer"/> - the imaging provider the Blob Management host
/// installs; without one, the picture is served as stored. Responses are cached by HTTP (the endpoint's <c>ETag</c> and
/// <c>Cache-Control</c>); there is no server-side cache of renditions. A width or height must be one of
/// the container's public sizes (<c>GetBlobManagementPublicSizes</c>: the container's own list, else the host's default
/// container's, else a ladder from 64 to 4096), otherwise the request is refused with
/// <see cref="AbpValidationException"/> before any bytes are read: the endpoint is anonymous, and an open
/// range of sizes would let a visitor make it render a distinct picture for every request. A size is never snapped to
/// the nearest allowed one - with both sides given the picture is cropped to the box, and snapping the sides
/// independently would change its shape. A picture larger than
/// <see cref="BlobManagementPublicUrl.MaxDimension"/> on a side, or whose header cannot be read, is served as stored rather than
/// decoded.
/// </para>
/// </summary>
[AllowAnonymous]
public class BlobManagementPublicAppService : BlobManagementAppService, IBlobManagementPublicAppService
{
    protected FileDescriptorManager FileDescriptorManager { get; }

    protected IImageResizer ImageResizer { get; }

    public BlobManagementPublicAppService(
        FileDescriptorManager fileDescriptorManager,
        IImageResizer imageResizer)
    {
        FileDescriptorManager = fileDescriptorManager;
        ImageResizer = imageResizer;
    }

    /// <summary>
    /// One descriptor per answer: where a file's name is its address a file can be saved over between two lookups, and
    /// a response built from both would carry the old file's tag and type with the new file's bytes. The tag, the
    /// not-modified decision, the information and the bytes all come from the one descriptor an answer is built on.
    /// <para>
    /// A save that lands between the lookup and the read of the bytes deletes the BLOB that was found. The file is
    /// not missing then: it is looked up once more, and the whole answer is built again from the descriptor it has
    /// now (<see cref="FileDescriptorManager.FindReplacedBlobAsync"/>).
    /// </para>
    /// </summary>
    [UnitOfWork(isTransactional: false)]
    public virtual async Task<BlobManagementPublicReadResult> ReadAsync(
        string containerName,
        string blobName,
        BlobManagementPublicReadInput input,
        BlobManagementPublicReadRequest request)
    {
        // Before the lookup: a size Blob Management does not serve is refused even for a missing file.
        CheckSize(containerName, input);

        var (file, name) = await GetPublicFileAsync(containerName, blobName);
        var result = await ReadAsync(file, name, input, request);
        if (result == null)
        {
            var replaced = await FileDescriptorManager.FindReplacedBlobAsync(file);
            if (replaced is { IsPublic: true })
            {
                result = await ReadAsync(replaced, replaced.Name, input, request);
            }
        }

        return result ?? throw new EntityNotFoundException(typeof(FileDescriptor), file.Id);
    }

    /// <summary>
    /// The answer built on <paramref name="file"/> alone, or <c>null</c> when its bytes are wanted and its BLOB is
    /// missing from storage.
    /// </summary>
    protected virtual async Task<BlobManagementPublicReadResult?> ReadAsync(
        FileDescriptor file,
        string name,
        BlobManagementPublicReadInput input,
        BlobManagementPublicReadRequest request)
    {
        var result = new BlobManagementPublicReadResult { File = MapReadFile(file, name, GetEntityTagSuffix(input)) };

        if (IsNotModified(result.File.EntityTag, request))
        {
            // Answered without reading the bytes.
            result.NotModified = true;
            return result;
        }

        if (request.HeadersOnly)
        {
            // The information without the bytes: nothing is read from storage, nothing is rendered.
            return result;
        }

        result.Content = await GetContentOrNullAsync(file, name, input);
        return result.Content == null ? null : result;
    }

    /// <summary>What a rendition adds to the entity tag of the file: <c>-{W}x{H}</c> when a size is asked.</summary>
    protected virtual string GetEntityTagSuffix(BlobManagementPublicReadInput input)
    {
        var width = input.Width ?? 0;
        var height = input.Height ?? 0;
        return width > 0 || height > 0
            ? "-" + width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
    }

    /// <summary>
    /// The bytes of <paramref name="file"/> - the very descriptor the read was decided on - or the picture resized when
    /// <paramref name="input"/> asks for a size - under <paramref name="name"/>, the name the address answers with
    /// (see <see cref="GetPublicFileAsync"/>); <c>null</c> when the BLOB is missing from storage.
    /// </summary>
    protected virtual async Task<IRemoteStreamContent?> GetContentOrNullAsync(FileDescriptor file, string name, BlobManagementPublicReadInput input)
    {
        var stream = await FileDescriptorManager.GetStreamOrNullAsync(file);
        if (stream == null)
        {
            return null;
        }

        int? width = input.Width > 0 ? input.Width : null;
        int? height = input.Height > 0 ? input.Height : null;
        if ((width != null || height != null) && IsResizable(file.MimeType))
        {
            // Read once: a storage stream need not be seekable, and the stored bytes are the fallback.
            byte[] bytes;
            await using (stream)
            {
                using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                bytes = buffer.ToArray();
            }

            var content = await TryResizeAsync(bytes, file.MimeType, width, height) ?? bytes;
            return new RemoteStreamContent(new MemoryStream(content, writable: false), name, file.MimeType, content.LongLength);
        }

        return new RemoteStreamContent(stream, name, file.MimeType, file.Size);
    }

    /// <summary>
    /// A width or height asked for (above 0) must be one of the container's public sizes and no larger
    /// than <see cref="BlobManagementPublicUrl.MaxDimension"/>; no size asked is always fine.
    /// </summary>
    protected virtual void CheckSize(string containerName, BlobManagementPublicReadInput input)
    {
        if (!(input.Width > 0) && !(input.Height > 0))
        {
            return;
        }

        var allowed = GetAllowedSizes(containerName);
        var errors = new List<ValidationResult>();
        if (input.Width > 0 && !IsAllowedSize(allowed, input.Width.Value))
        {
            errors.Add(new ValidationResult(SizeMessage(allowed, nameof(input.Width)), new[] { nameof(input.Width) }));
        }

        if (input.Height > 0 && !IsAllowedSize(allowed, input.Height.Value))
        {
            errors.Add(new ValidationResult(SizeMessage(allowed, nameof(input.Height)), new[] { nameof(input.Height) }));
        }

        if (errors.Count > 0)
        {
            throw new AbpValidationException(string.Join(" ", errors.Select(error => error.ErrorMessage)), errors);
        }
    }

    /// <summary>
    /// The sizes the container's pictures are served at: its own list, else the default container's, else the module's.
    /// A container that is not managed answers with the default's: its address is not found anyway, and a size it
    /// refuses tells nothing more.
    /// </summary>
    protected virtual IReadOnlyList<int> GetAllowedSizes(string containerName)
    {
        return ContainerConfigurationProvider.Get(containerName).GetBlobManagementPublicSizes();
    }

    protected virtual bool IsAllowedSize(IReadOnlyList<int> allowed, int size)
    {
        return size <= BlobManagementPublicUrl.MaxDimension && allowed.Contains(size);
    }

    protected virtual string SizeMessage(IReadOnlyList<int> allowed, string side)
    {
        return L["PublicReadSizeNotAllowed", side, string.Join(", ", allowed)];
    }

    /// <summary>
    /// The live public file at the address of a managed container, and the name the address answers with - the file's.
    /// The address of a file an application saved names it as the application did; that of a file kept by id, whose
    /// name may repeat, names its physical BLOB - generated, so it is never mistaken for an application's name, and no
    /// other file's. A file that is not public answers as a missing one: whether it is there is not the visitor's to
    /// know.
    /// </summary>
    protected virtual async Task<(FileDescriptor File, string Name)> GetPublicFileAsync(string containerName, string blobName)
    {
        // A container Blob Management does not manage is as not found as a missing file: the endpoint tells nothing
        // about the host.
        if (IsManagedContainer(containerName))
        {
            var saved = await FileDescriptorManager.FindBlobAsync(containerName, blobName);
            if (saved is { IsPublic: true })
            {
                return (saved, saved.Name);
            }

            var kept = await FileDescriptorManager.FindPublicByBlobNameAsync(containerName, blobName);
            if (kept != null)
            {
                return (kept, kept.Name);
            }
        }

        throw new EntityNotFoundException(typeof(FileDescriptor), $"{containerName}/{blobName}");
    }

    /// <summary>
    /// JPEG, PNG and WebP, unless animated (<see cref="TryResizeAsync"/>); a GIF would lose its animation, anything else is
    /// not a picture.
    /// </summary>
    protected virtual bool IsResizable(string mimeType)
    {
        return mimeType is "image/jpeg" or "image/png" or "image/webp";
    }

    /// <summary>
    /// The picture at the size asked, or <c>null</c> to serve it as stored: it is no larger than asked (nothing to
    /// shrink), it is animated (an APNG, an animated WebP: resized, it would keep one frame), it is too large or unreadable
    /// to decode safely, or no imaging provider handles it.
    /// </summary>
    protected virtual async Task<byte[]?> TryResizeAsync(byte[] bytes, string mimeType, int? width, int? height)
    {
        using var probe = new MemoryStream(bytes, writable: false);
        if (ImageHeaderReader.IsAnimated(probe))
        {
            return null;
        }

        var dimensions = ImageHeaderReader.Read(probe);
        if (dimensions == null ||
            dimensions.Value.Width > BlobManagementPublicUrl.MaxDimension ||
            dimensions.Value.Height > BlobManagementPublicUrl.MaxDimension)
        {
            return null;
        }

        ImageResizeMode mode;
        if (width != null && height != null)
        {
            mode = ImageResizeMode.Crop;
        }
        else if ((width ?? int.MaxValue) >= dimensions.Value.Width && (height ?? int.MaxValue) >= dimensions.Value.Height)
        {
            // One side asked, and the picture is already no larger: never enlarge.
            return null;
        }
        else
        {
            mode = ImageResizeMode.Max;
        }

        var result = await ImageResizer.ResizeAsync(
            bytes,
            new ImageResizeArgs((uint?)width ?? 0, (uint?)height ?? 0, mode),
            mimeType);

        return result.State == ImageProcessState.Done ? result.Result : null;
    }
}
