using System;
using System.Threading.Tasks;
using Dignite.Drive.Features;
using Dignite.Drive.Permissions;
using Dignite.Drive.Tickets;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Authorization;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Features;

namespace Dignite.Drive.Files;

/// <summary>
/// The files of the user drives. Any signed-in user reads a file by its id; uploading takes
/// <see cref="DrivePermissions.Files.Upload"/>, and deleting <see cref="DrivePermissions.Files.Delete"/> unless the caller
/// uploaded the file. Storing, dedup and the quota are <see cref="FileDescriptorManager"/>'s. A file of a system container is
/// not found here, whoever asks: those are the applications', reached through the BLOB storing provider. Part of the
/// tenant's Drive: with <see cref="DriveFeatures.Enable"/> off it refuses every call.
/// </summary>
[Authorize]
[RequiresFeature(DriveFeatures.Enable)]
public class DriveFileAppService : DriveAppService, IDriveFileAppService
{
    protected IFileDescriptorRepository FileRepository { get; }

    protected FileDescriptorManager FileDescriptorManager { get; }

    public DriveFileAppService(
        IFileDescriptorRepository fileRepository,
        FileDescriptorManager fileDescriptorManager)
    {
        FileRepository = fileRepository;
        FileDescriptorManager = fileDescriptorManager;
    }

    public virtual async Task<DriveFileDto> GetAsync(Guid id)
    {
        GetUserId();
        return Map(await GetFileAsync(id));
    }

    public virtual async Task<DriveFileDto> CreateAsync(CreateDriveFileInput input)
    {
        var userId = GetUserId();
        await AuthorizeUploadAsync(input.ContainerName);

        // The content closes what it owns; the request's own body is the server's to close.
        using var content = input.File;
        var name = !input.Name.IsNullOrWhiteSpace() ? input.Name!.Trim()
            : !content.FileName.IsNullOrWhiteSpace() ? content.FileName!.Trim()
            : "file";

        var file = await FileDescriptorManager.UploadAsync(input.ContainerName, name, content.GetStream());
        return Map(file);
    }

    public virtual async Task<DriveTicketDto> CreateUploadTicketAsync(CreateDriveFileUploadTicketInput input)
    {
        var userId = GetUserId();
        await AuthorizeUploadAsync(input.ContainerName);

        var protector = LazyServiceProvider.LazyGetRequiredService<DriveTicketProtector>();
        var ticket = new DriveTicket
        {
            Kind = DriveTicketKind.FileUpload,
            TenantId = CurrentTenant.Id,
            ContainerName = input.ContainerName,
            BlobName = string.Empty,
            FileName = input.FileName.Trim(),
            UserId = userId,
            UserName = CurrentUser.UserName,
            Roles = CurrentUser.Roles,
            Nonce = Guid.NewGuid().ToString("N"),
            ExpiresAt = protector.ExpiresAfter(input.LifetimeSeconds)
        };

        var protectedTicket = protector.Protect(ticket);
        return new DriveTicketDto
        {
            Ticket = protectedTicket,
            Kind = ticket.Kind,
            ExpiresAt = ticket.ExpiresAt,
            Url = DriveTicketUrl.BuildFileUpload(protectedTicket)
        };
    }

    /// <summary>The uploader deletes their own file; anyone else needs <see cref="DrivePermissions.Files.Delete"/>.</summary>
    public virtual async Task DeleteAsync(Guid id)
    {
        var userId = GetUserId();
        var file = await FileRepository.FindAsync(id);
        if (file == null || !IsUserDrive(file.ContainerName))
        {
            return;
        }

        if (file.CreatorId != userId)
        {
            await AuthorizationService.CheckAsync(DrivePermissions.Files.Delete);
        }

        await FileDescriptorManager.DeleteAsync(file);
    }

    public virtual async Task<IRemoteStreamContent> GetContentAsync(Guid id)
    {
        GetUserId();
        var file = await GetFileAsync(id);
        var stream = await FileDescriptorManager.GetStreamOrNullAsync(file)
                     ?? throw new EntityNotFoundException(typeof(FileDescriptor), id);
        return new RemoteStreamContent(stream, file.Name, file.MimeType, file.Size);
    }

    /// <summary>
    /// Whether the caller may upload to the user drive <paramref name="containerName"/> - the same check for an upload
    /// and for a ticket that will make one. The container is checked first, so an unknown one is not found whoever asks.
    /// </summary>
    protected virtual async Task AuthorizeUploadAsync(string containerName)
    {
        GetUserDrive(containerName);
        await AuthorizationService.CheckAsync(DrivePermissions.Files.Upload);
    }

    /// <summary>A file of a user drive; a system container's file is not found.</summary>
    protected virtual async Task<FileDescriptor> GetFileAsync(Guid id)
    {
        var file = await FileRepository.FindAsync(id);
        if (file == null || !IsUserDrive(file.ContainerName))
        {
            throw new EntityNotFoundException(typeof(FileDescriptor), id);
        }

        return file;
    }
}
