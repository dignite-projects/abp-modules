using System;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobManagement.Permissions;
using Dignite.Abp.BlobManagement.Tickets;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Authorization;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Features;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The files of the user drives. Any signed-in user reads a file by its id; uploading takes
/// <see cref="BlobManagementPermissions.Files.Upload"/>, and deleting <see cref="BlobManagementPermissions.Files.Delete"/> unless the caller
/// uploaded the file. Storing, dedup and the quota are <see cref="FileDescriptorManager"/>'s. A file of a system container is
/// not found here, whoever asks: those are the applications', reached through the BLOB storing provider. Part of the
/// tenant's BlobManagement: with <see cref="BlobManagementFeatures.Enable"/> off it refuses every call.
/// </summary>
[Authorize]
[RequiresFeature(BlobManagementFeatures.Enable)]
public class BlobManagementFileAppService : BlobManagementAppService, IBlobManagementFileAppService
{
    protected IFileDescriptorRepository FileRepository { get; }

    protected FileDescriptorManager FileDescriptorManager { get; }

    public BlobManagementFileAppService(
        IFileDescriptorRepository fileRepository,
        FileDescriptorManager fileDescriptorManager)
    {
        FileRepository = fileRepository;
        FileDescriptorManager = fileDescriptorManager;
    }

    public virtual async Task<BlobManagementFileDto> GetAsync(Guid id)
    {
        GetUserId();
        return Map(await GetFileAsync(id));
    }

    public virtual async Task<BlobManagementFileDto> CreateAsync(CreateBlobManagementFileInput input)
    {
        var userId = GetUserId();
        await AuthorizeUploadAsync(input.ContainerName);

        // The content closes what it owns; the request's own body is the server's to close.
        using var content = input.File;
        var name = !input.Name.IsNullOrWhiteSpace() ? input.Name!.Trim()
            : !content.FileName.IsNullOrWhiteSpace() ? content.FileName!.Trim()
            : "file";

        var configureFile = await PrepareNewFileAsync(input);
        var file = await FileDescriptorManager.UploadAsync(input.ContainerName, name, content.GetStream(), configureFile);
        return Map(file);
    }

    public virtual async Task<BlobManagementTicketDto> CreateUploadTicketAsync(CreateBlobManagementFileUploadTicketInput input)
    {
        var userId = GetUserId();
        await AuthorizeUploadAsync(input.ContainerName);

        var protector = LazyServiceProvider.LazyGetRequiredService<BlobManagementTicketProtector>();
        var ticket = new BlobManagementTicket
        {
            Kind = BlobManagementTicketKind.FileUpload,
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
        return new BlobManagementTicketDto
        {
            Ticket = protectedTicket,
            Kind = ticket.Kind,
            ExpiresAt = ticket.ExpiresAt,
            Url = BlobManagementTicketUrl.BuildFileUpload(protectedTicket)
        };
    }

    /// <summary>The uploader deletes their own file; anyone else needs <see cref="BlobManagementPermissions.Files.Delete"/>.</summary>
    public virtual async Task DeleteAsync(Guid id)
    {
        var userId = GetUserId();
        var file = await FileRepository.FindAsync(id);
        if (file == null || !IsUserBlobManagement(file.ContainerName))
        {
            return;
        }

        if (file.CreatorId != userId)
        {
            await AuthorizationService.CheckAsync(BlobManagementPermissions.Files.Delete);
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
        GetUserBlobManagement(containerName);
        await AuthorizationService.CheckAsync(BlobManagementPermissions.Files.Upload);
    }

    /// <summary>
    /// What to set on the new file before it is inserted, worked out once the upload is authorized: an implementation
    /// that keeps fields of its own on the file (ABP object extensions) returns the action that sets them, and
    /// <see cref="FileDescriptorManager.UploadAsync"/> runs it on the new descriptor - on the storing and the dedup path
    /// alike - so the row is written once. Anything it has to read, it reads here; the action itself runs under the dedup
    /// lock and does no I/O. Default: nothing.
    /// </summary>
    protected virtual Task<Action<FileDescriptor>?> PrepareNewFileAsync(CreateBlobManagementFileInput input)
    {
        return Task.FromResult<Action<FileDescriptor>?>(null);
    }

    /// <summary>A file of a user drive; a system container's file is not found.</summary>
    protected virtual async Task<FileDescriptor> GetFileAsync(Guid id)
    {
        var file = await FileRepository.FindAsync(id);
        if (file == null || !IsUserBlobManagement(file.ContainerName))
        {
            throw new EntityNotFoundException(typeof(FileDescriptor), id);
        }

        return file;
    }
}
