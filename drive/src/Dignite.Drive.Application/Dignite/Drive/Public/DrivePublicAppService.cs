using System;
using System.IO;
using System.Threading.Tasks;
using Dignite.Abp.BlobStoring.Imaging;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Imaging;

namespace Dignite.Drive.Public;

/// <summary>
/// Serves the files of <c>PublicRead</c> containers to anyone, in the request's tenant (an anonymous request names it
/// with <c>?__tenant=</c>, see <see cref="DrivePublicUrl"/>). Not gated by <c>Drive.Enable</c>: turning the Drive off
/// stops a tenant storing, not its published files being read.
/// <para>
/// A size is rendered on every request with ABP's <see cref="IImageResizer"/> - the imaging provider the Drive host
/// installs; without one, the picture is served as stored. Responses are cached by HTTP (the endpoint's <c>ETag</c> and
/// <c>Cache-Control</c>); there is no server-side cache of renditions. A picture larger than
/// <see cref="DrivePublicUrl.MaxDimension"/> on a side, or whose header cannot be read, is served as stored rather than
/// decoded.
/// </para>
/// </summary>
[AllowAnonymous]
public class DrivePublicAppService : DriveAppService, IDrivePublicAppService
{
    protected FileDescriptorManager FileDescriptorManager { get; }

    protected IImageResizer ImageResizer { get; }

    public DrivePublicAppService(FileDescriptorManager fileDescriptorManager, IImageResizer imageResizer)
    {
        FileDescriptorManager = fileDescriptorManager;
        ImageResizer = imageResizer;
    }

    public virtual async Task<DrivePublicFileDto> GetInfoAsync(string containerName, string blobName)
    {
        var file = await GetPublicFileAsync(containerName, blobName);
        return new DrivePublicFileDto
        {
            Name = file.Name,
            MimeType = file.MimeType,
            Size = file.Size,
            ContentId = file.BlobName
        };
    }

    public virtual async Task<IRemoteStreamContent> GetAsync(string containerName, string blobName, DrivePublicReadInput input)
    {
        var file = await GetPublicFileAsync(containerName, blobName);
        var stream = await FileDescriptorManager.GetStreamOrNullAsync(file)
                     ?? throw new EntityNotFoundException(typeof(FileDescriptor), $"{containerName}/{blobName}");

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

    /// <summary>The live file at the address, when its container is a Drive container that is read publicly.</summary>
    protected virtual async Task<FileDescriptor> GetPublicFileAsync(string containerName, string blobName)
    {
        FileDescriptor? file = null;
        try
        {
            if (FileDescriptorManager.GetDriveContainerConfiguration(containerName).PublicRead)
            {
                file = await FileDescriptorManager.FindAsync(containerName, blobName);
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
