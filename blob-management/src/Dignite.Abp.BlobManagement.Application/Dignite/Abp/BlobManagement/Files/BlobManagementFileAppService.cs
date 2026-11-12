using System;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Authorization;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobManagement.Tickets;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Features;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The files of the managed containers. What a caller may do with files is the container's permission, at a place
/// the caller may view (<see cref="BlobManagementAccessService"/>): it is worked out for the place, and no question is
/// asked for each file. Reading one file by its id takes a signed-in user and nothing else - the id is what a business
/// system keeps and hands out under its own authorization - and the uploader of a file may delete it. Storing, dedup
/// and the quota are <see cref="FileDescriptorManager"/>'s. Part of the tenant's Blob Management: with
/// <see cref="BlobManagementFeatures.Enable"/> off it refuses every call.
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

    public virtual async Task<PagedResultDto<BlobManagementFileDto>> GetListAsync(GetBlobManagementFilesInput input)
    {
        GetUserId();
        GetContainer(input.ContainerName);
        Authorize((await GetAccessAsync(input.ContainerName, input.DirectoryId)).View);

        var count = await FileRepository.GetCountAsync(input.ContainerName, input.DirectoryId, input.Filter);
        var files = await FileRepository.GetListAsync(
            input.ContainerName,
            input.DirectoryId,
            input.Filter,
            input.Sorting,
            input.MaxResultCount,
            input.SkipCount);

        return new PagedResultDto<BlobManagementFileDto>(count, files.Select(Map).ToList());
    }

    public virtual async Task<BlobManagementFileDto> CreateAsync(CreateBlobManagementFileInput input)
    {
        GetUserId();
        await AuthorizeUploadAsync(input.ContainerName, input.DirectoryId);

        // The content closes what it owns; the request's own body is the server's to close.
        using var content = input.File;
        var name = !input.Name.IsNullOrWhiteSpace() ? input.Name!.Trim()
            : !content.FileName.IsNullOrWhiteSpace() ? content.FileName!.Trim()
            : "file";

        var file = await FileDescriptorManager.UploadAsync(input.ContainerName, name, content.GetStream(), input.DirectoryId);
        return Map(file);
    }

    public virtual async Task<BlobManagementTicketDto> CreateUploadTicketAsync(CreateBlobManagementFileUploadTicketInput input)
    {
        var userId = GetUserId();
        await AuthorizeUploadAsync(input.ContainerName, input.DirectoryId);

        var protector = LazyServiceProvider.LazyGetRequiredService<BlobManagementTicketProtector>();
        var ticket = new BlobManagementTicket
        {
            Kind = BlobManagementTicketKind.FileUpload,
            TenantId = CurrentTenant.Id,
            ContainerName = input.ContainerName,
            DirectoryId = input.DirectoryId,
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

    public virtual async Task<BlobManagementFileDto> UpdateAsync(Guid id, UpdateBlobManagementFileInput input)
    {
        GetUserId();
        var file = await GetFileAsync(id);
        Authorize((await GetAccessAsync(file)).UpdateFiles);

        await FileDescriptorManager.RenameAsync(file, input.Name.Trim());
        return Map(file);
    }

    /// <summary>
    /// The caller needs to rename and move files where the file is, and to upload where it lands.
    /// </summary>
    public virtual async Task<BlobManagementFileDto> MoveAsync(Guid id, MoveBlobManagementFileInput input)
    {
        GetUserId();
        var file = await GetFileAsync(id);
        Authorize((await GetAccessAsync(file)).UpdateFiles);
        await AuthorizeUploadAsync(file.ContainerName, input.DirectoryId);

        await FileDescriptorManager.MoveAsync(file, input.DirectoryId);
        return Map(file);
    }

    /// <summary>The uploader deletes their own file; anyone else needs to delete files where it is.</summary>
    public virtual async Task DeleteAsync(Guid id)
    {
        var userId = GetUserId();
        var file = await FileRepository.FindAsync(id);
        if (file == null || !IsManagedContainer(file.ContainerName))
        {
            return;
        }

        if (file.CreatorId != userId)
        {
            Authorize((await GetAccessAsync(file)).DeleteFiles);
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
    /// Whether the caller may upload to the place - the same check for an upload, for a ticket that will make one and
    /// for where a moved file lands. The container is checked first, so an unknown one is not found whoever asks.
    /// </summary>
    protected virtual async Task AuthorizeUploadAsync(string containerName, Guid? directoryId)
    {
        GetContainer(containerName);
        Authorize((await GetAccessAsync(containerName, directoryId)).UploadFiles);
    }

    /// <summary>What the caller may do at a place of <paramref name="containerName"/>; a directory of another container is not found.</summary>
    protected virtual async Task<BlobManagementAccess> GetAccessAsync(string containerName, Guid? directoryId)
    {
        var directory = directoryId.HasValue ? await GetDirectoryAsync(directoryId.Value, containerName) : null;
        return await AccessService.GetAccessAsync(await AccessService.BeginAsync(), containerName, directory);
    }

    /// <summary>
    /// What the caller may do where <paramref name="file"/> is. A file whose directory is gone - deleted while the file
    /// was being uploaded into it - is answered by its container.
    /// </summary>
    protected virtual async Task<BlobManagementAccess> GetAccessAsync(FileDescriptor file)
    {
        var directory = file.DirectoryId.HasValue ? await DirectoryRepository.FindAsync(file.DirectoryId.Value) : null;
        return await AccessService.GetAccessAsync(await AccessService.BeginAsync(), file.ContainerName, directory);
    }

    /// <summary>A file of a managed container; any other is not found.</summary>
    protected virtual async Task<FileDescriptor> GetFileAsync(Guid id)
    {
        var file = await FileRepository.FindAsync(id);
        if (file == null || !IsManagedContainer(file.ContainerName))
        {
            throw new EntityNotFoundException(typeof(FileDescriptor), id);
        }

        return file;
    }
}
