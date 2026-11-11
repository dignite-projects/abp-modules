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

namespace Dignite.Drive.Public;

/// <summary>
/// Serves the files of <c>PublicRead</c> containers to anyone, in the request's tenant (an anonymous request names it
/// with <c>?__tenant=</c>, see <see cref="DrivePublicUrl"/>). Not gated by <c>Drive.Enable</c>: turning the Drive off
/// stops a tenant storing, not its published files being read.
/// <para>
/// A size is rendered on every request with ABP's <see cref="IImageResizer"/> - the imaging provider the Drive host
/// installs; without one, the picture is served as stored. Responses are cached by HTTP (the endpoint's <c>ETag</c> and
/// <c>Cache-Control</c>); there is no server-side cache of renditions. A width or height must be one of
/// <see cref="DrivePublicReadOptions.AllowedSizes"/> (a ladder from 64 to 4096 by default), otherwise the request is
/// refused with <see cref="AbpValidationException"/> before any bytes are read: the endpoint is anonymous, and an open
/// range of sizes would let a visitor make it render a distinct picture for every request. A size is never snapped to
/// the nearest allowed one - with both sides given the picture is cropped to the box, and snapping the sides
/// independently would change its shape. A picture larger than
/// <see cref="DrivePublicUrl.MaxDimension"/> on a side, or whose header cannot be read, is served as stored rather than
/// decoded.
/// </para>
/// </summary>
[AllowAnonymous]
public class DrivePublicAppService : DriveAppService, IDrivePublicAppService
{
    protected FileDescriptorManager FileDescriptorManager { get; }

    protected IImageResizer ImageResizer { get; }

    protected DrivePublicReadOptions ReadOptions { get; }

    public DrivePublicAppService(
        FileDescriptorManager fileDescriptorManager,
        IImageResizer imageResizer,
        IOptions<DrivePublicReadOptions> options)
    {
        FileDescriptorManager = fileDescriptorManager;
        ImageResizer = imageResizer;
        ReadOptions = options.Value;
    }

    /// <summary>
    /// One lookup per read: in a system container a file's name is its address, so a file can be overwritten between
    /// two lookups, and a response built from both would carry the old file's tag and type with the new file's bytes.
    /// The tag, the not-modified decision, the information and the bytes all come from the one descriptor found here.
    /// </summary>
    [UnitOfWork(isTransactional: false)]
    public virtual async Task<DrivePublicReadResult> ReadAsync(
        string containerName,
        string blobName,
        DrivePublicReadInput input,
        DrivePublicReadRequest request)
    {
        // Before the lookup: a size the Drive does not serve is refused even for a missing file.
        CheckSize(input);

        var file = await GetPublicFileAsync(containerName, blobName);
        var result = new DrivePublicReadResult
        {
            File = new DrivePublicFileDto
            {
                Name = file.Name,
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

        result.Content = await GetContentAsync(file, input);
        return result;
    }

    /// <summary>
    /// The opaque value of the entity tag of <paramref name="file"/> read at <paramref name="input"/>'s size: the
    /// physical BLOB name, which an overwrite changes, plus <c>-{W}x{H}</c> when a size is asked. Quoted-string
    /// content may not contain <c>"</c> or <c>\</c>; a BLOB name is generated, but they are removed anyway.
    /// </summary>
    protected virtual string CreateEntityTag(FileDescriptor file, DrivePublicReadInput? input)
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
    protected virtual bool IsNotModified(string entityTag, DrivePublicReadRequest? request)
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
    /// <paramref name="input"/> asks for a size. A BLOB missing from storage is <see cref="EntityNotFoundException"/>.
    /// </summary>
    protected virtual async Task<IRemoteStreamContent> GetContentAsync(FileDescriptor file, DrivePublicReadInput input)
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
            return new RemoteStreamContent(new MemoryStream(content, writable: false), FileName(file), file.MimeType, content.LongLength);
        }

        return new RemoteStreamContent(stream, FileName(file), file.MimeType, file.Size);
    }

    /// <summary>
    /// A width or height asked for (above 0) must be in <see cref="DrivePublicReadOptions.AllowedSizes"/> and no larger
    /// than <see cref="DrivePublicUrl.MaxDimension"/>; no size asked is always fine.
    /// </summary>
    protected virtual void CheckSize(DrivePublicReadInput? input)
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
        return size <= DrivePublicUrl.MaxDimension && ReadOptions.AllowedSizes.Contains(size);
    }

    protected virtual string SizeMessage(string side)
    {
        var allowed = ReadOptions.AllowedSizes
            .Where(size => size > 0 && size <= DrivePublicUrl.MaxDimension)
            .Distinct()
            .OrderBy(size => size);
        return L["PublicReadSizeNotAllowed", side, string.Join(", ", allowed)];
    }

    /// <summary>
    /// The live file at the address, when its container is a Drive container that is read publicly. In a system
    /// container the address names the file as the application named it; in a user drive, where names may repeat, it
    /// names the physical BLOB.
    /// </summary>
    protected virtual async Task<FileDescriptor> GetPublicFileAsync(string containerName, string blobName)
    {
        FileDescriptor? file = null;
        try
        {
            var drive = FileDescriptorManager.GetDriveContainerConfiguration(containerName);
            if (drive.PublicRead)
            {
                file = drive.Kind == DriveContainerKind.System
                    ? await FileDescriptorManager.FindAsync(containerName, blobName)
                    : await FileDescriptorManager.FindByBlobNameAsync(containerName, blobName);
            }
        }
        catch (BusinessException exception) when (exception.Code == DriveErrorCodes.ContainerNotFound)
        {
            // Not a Drive container: as not found as a missing file, so the endpoint tells nothing about the host.
        }

        return file ?? throw new EntityNotFoundException(typeof(FileDescriptor), $"{containerName}/{blobName}");
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
            dimensions.Value.Width > DrivePublicUrl.MaxDimension ||
            dimensions.Value.Height > DrivePublicUrl.MaxDimension)
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

    protected virtual string FileName(FileDescriptor file)
    {
        var slash = file.Name.LastIndexOf('/');
        return slash < 0 ? file.Name : file.Name.Substring(slash + 1);
    }
}
