using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
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
/// <see cref="BlobManagementPublicReadOptions.AllowedSizes"/> (a ladder from 64 to 4096 by default), otherwise the request is
/// refused with <see cref="AbpValidationException"/> before any bytes are read: the endpoint is anonymous, and an open
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

    protected BlobManagementPublicReadOptions ReadOptions { get; }

    public BlobManagementPublicAppService(
        FileDescriptorManager fileDescriptorManager,
        IImageResizer imageResizer,
        IOptions<BlobManagementPublicReadOptions> options)
    {
        FileDescriptorManager = fileDescriptorManager;
        ImageResizer = imageResizer;
        ReadOptions = options.Value;
    }

    /// <summary>
    /// One lookup per read: where a file's name is its address a file can be overwritten between
    /// two lookups, and a response built from both would carry the old file's tag and type with the new file's bytes.
    /// The tag, the not-modified decision, the information and the bytes all come from the one descriptor found here.
    /// </summary>
    [UnitOfWork(isTransactional: false)]
    public virtual async Task<BlobManagementPublicReadResult> ReadAsync(
        string containerName,
        string blobName,
        BlobManagementPublicReadInput input,
        BlobManagementPublicReadRequest request)
    {
        // Before the lookup: a size Blob Management does not serve is refused even for a missing file.
        CheckSize(input);

        var (file, name) = await GetPublicFileAsync(containerName, blobName);
        var result = new BlobManagementPublicReadResult
        {
            File = new BlobManagementPublicFileDto
            {
                Name = name,
                MimeType = file.MimeType,
                Size = file.Size,
                ContentId = file.BlobName,
                EntityTag = CreateEntityTag(file, input)
            }
        };

        if (IsNotModified(result.File.EntityTag, request))
        {
            // Answered without reading the bytes.
            result.NotModified = true;
            return result;
        }

        if (request?.HeadersOnly == true)
        {
            // The information without the bytes: nothing is read from storage, nothing is rendered.
            return result;
        }

        result.Content = await GetContentAsync(file, name, input);
        return result;
    }

    /// <summary>
    /// The opaque value of the entity tag of <paramref name="file"/> read at <paramref name="input"/>'s size: the
    /// physical BLOB name, which an overwrite changes, plus <c>-{W}x{H}</c> when a size is asked. Quoted-string
    /// content may not contain <c>"</c> or <c>\</c>; a BLOB name is generated, but they are removed anyway.
    /// </summary>
    protected virtual string CreateEntityTag(FileDescriptor file, BlobManagementPublicReadInput? input)
    {
        var width = input?.Width ?? 0;
        var height = input?.Height ?? 0;
        var size = width > 0 || height > 0
            ? "-" + width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        return (file.BlobName + size).Replace("\"", string.Empty).Replace("\\", string.Empty);
    }

    /// <summary>
    /// Weak comparison, as <c>If-None-Match</c> uses: <c>*</c> matches any file, and a tag matches when its opaque
    /// value is the same (the caller has already dropped a <c>W/</c> prefix).
    /// </summary>
    protected virtual bool IsNotModified(string entityTag, BlobManagementPublicReadRequest? request)
    {
        if (request == null)
        {
            return false;
        }

        return request.IfNoneMatchAny ||
               (request.IfNoneMatch?.Any(tag => string.Equals(tag, entityTag, StringComparison.Ordinal)) ?? false);
    }

    /// <summary>
    /// The bytes of <paramref name="file"/> - the very descriptor the read was decided on - or the picture resized when
    /// <paramref name="input"/> asks for a size - under <paramref name="name"/>, the name the address answers with
    /// (see <see cref="GetPublicFileAsync"/>). A BLOB missing from storage is <see cref="EntityNotFoundException"/>.
    /// </summary>
    protected virtual async Task<IRemoteStreamContent> GetContentAsync(FileDescriptor file, string name, BlobManagementPublicReadInput input)
    {
        var stream = await FileDescriptorManager.GetStreamOrNullAsync(file)
                     ?? throw new EntityNotFoundException(typeof(FileDescriptor), file.Id);

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
            return new RemoteStreamContent(new MemoryStream(content, writable: false), FileName(name), file.MimeType, content.LongLength);
        }

        return new RemoteStreamContent(stream, FileName(name), file.MimeType, file.Size);
    }

    /// <summary>
    /// A width or height asked for (above 0) must be in <see cref="BlobManagementPublicReadOptions.AllowedSizes"/> and no larger
    /// than <see cref="BlobManagementPublicUrl.MaxDimension"/>; no size asked is always fine.
    /// </summary>
    protected virtual void CheckSize(BlobManagementPublicReadInput? input)
    {
        var errors = new List<ValidationResult>();
        if (input?.Width > 0 && !IsAllowedSize(input.Width.Value))
        {
            errors.Add(new ValidationResult(SizeMessage(nameof(input.Width)), new[] { nameof(input.Width) }));
        }

        if (input?.Height > 0 && !IsAllowedSize(input.Height.Value))
        {
            errors.Add(new ValidationResult(SizeMessage(nameof(input.Height)), new[] { nameof(input.Height) }));
        }

        if (errors.Count > 0)
        {
            throw new AbpValidationException(string.Join(" ", errors.Select(error => error.ErrorMessage)), errors);
        }
    }

    protected virtual bool IsAllowedSize(int size)
    {
        return size <= BlobManagementPublicUrl.MaxDimension && ReadOptions.AllowedSizes.Contains(size);
    }

    protected virtual string SizeMessage(string side)
    {
        var allowed = ReadOptions.AllowedSizes
            .Where(size => size > 0 && size <= BlobManagementPublicUrl.MaxDimension)
            .Distinct()
            .OrderBy(size => size);
        return L["PublicReadSizeNotAllowed", side, string.Join(", ", allowed)];
    }

    /// <summary>
    /// The live public file at the address of a managed container, and the name the address answers with. The address
    /// of a file an application saved names it as the application did; that of a file kept by id, whose name may
    /// repeat, names its physical BLOB - generated, so it is never mistaken for an application's name.
    /// <para>
    /// An address that names a BLOB names content, not one file: files kept by id that hold the same bytes share the
    /// BLOB, so it is the address of every one of them, and it answers for as long as one of them is there and public.
    /// Their bytes, type and size are the same; their names are not, and the address does not say which file it was
    /// handed out for. So it answers with the file's name only when a single public file has the BLOB, and with the
    /// BLOB's own name otherwise - never with the name another file was given. A file that is not public answers as
    /// a missing one: whether it is there is not the visitor's to know.
    /// </para>
    /// </summary>
    protected virtual async Task<(FileDescriptor File, string Name)> GetPublicFileAsync(string containerName, string blobName)
    {
        try
        {
            var saved = await FileDescriptorManager.FindAsync(containerName, blobName);
            if (saved is { IsPublic: true })
            {
                return (saved, saved.Name);
            }

            // Two are enough to know whether the first is the only one.
            var files = await FileDescriptorManager.GetPublicListByBlobNameAsync(containerName, blobName, maxResultCount: 2);
            if (files.Count > 0)
            {
                return (files[0], files.Count == 1 ? files[0].Name : files[0].BlobName);
            }
        }
        catch (BusinessException exception) when (exception.Code == BlobManagementErrorCodes.ContainerNotFound)
        {
            // Not a Blob Management container: as not found as a missing file, so the endpoint tells nothing about the host.
        }

        throw new EntityNotFoundException(typeof(FileDescriptor), $"{containerName}/{blobName}");
    }

    /// <summary>JPEG, PNG and WebP; a GIF would lose its animation, anything else is not a picture.</summary>
    protected virtual bool IsResizable(string mimeType)
    {
        return mimeType is "image/jpeg" or "image/png" or "image/webp";
    }

    /// <summary>
    /// The picture at the size asked, or <c>null</c> to serve it as stored: it is no larger than asked (nothing to
    /// shrink), it is too large or unreadable to decode safely, or no imaging provider handles it.
    /// </summary>
    protected virtual async Task<byte[]?> TryResizeAsync(byte[] bytes, string mimeType, int? width, int? height)
    {
        using var probe = new MemoryStream(bytes, writable: false);
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

    protected virtual string FileName(string name)
    {
        var slash = name.LastIndexOf('/');
        return slash < 0 ? name : name.Substring(slash + 1);
    }
}
