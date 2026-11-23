using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// What the file services share - the trusted in-process <see cref="IBlobManagementFileService"/>, the integration
/// service behind an authorization of the client, and the reads of the management page: how the files of a place are
/// listed, a file is stored and its bytes are opened, on <see cref="FileDescriptorManager"/>.
/// <para>
/// It has no public method on purpose. Each service writes every method of its contract itself, so one that is added
/// to a contract cannot be reached over HTTP before someone has written how its caller is authorized.
/// </para>
/// </summary>
public abstract class BlobManagementFileServiceBase : BlobManagementAppService
{
    protected IFileDescriptorRepository FileRepository { get; }

    protected FileDescriptorManager FileDescriptorManager { get; }

    protected BlobManagementFileServiceBase(
        IFileDescriptorRepository fileRepository,
        FileDescriptorManager fileDescriptorManager)
    {
        FileRepository = fileRepository;
        FileDescriptorManager = fileDescriptorManager;
    }

    /// <summary>The confirmed files of the place <paramref name="input"/> names, a page at a time.</summary>
    protected virtual async Task<PagedResultDto<BlobManagementFileDto>> ListFilesAsync(GetBlobManagementFilesInput input)
    {
        await CheckPlaceAsync(input.ContainerName, input.DirectoryId);

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

    /// <summary>
    /// Stores the file, recording <paramref name="clientId"/> as its writer when a remote client sent it. The user it
    /// is counted for is the one the caller named; without one, ABP's auditing records the current user.
    /// </summary>
    protected virtual async Task<BlobManagementFileDto> StoreFileAsync(CreateBlobManagementFileInput input, string? clientId)
    {
        Check.NotNull(input.File, nameof(input.File));

        // The content closes what it owns; the request's own body is the server's to close.
        using var content = input.File;
        var name = !input.Name.IsNullOrWhiteSpace() ? input.Name!.Trim()
            : !content.FileName.IsNullOrWhiteSpace() ? content.FileName!.Trim()
            : "file";

        // The input's own name was validated with the input; the uploaded file's was not.
        CheckNameLength(name, nameof(input.Name));

        var file = await FileDescriptorManager.CreateFileAsync(
            input.ContainerName,
            name,
            content.GetStream(),
            new CreateFileArgs
            {
                DirectoryId = input.DirectoryId,
                IsPublic = input.IsPublic,
                CreatorId = input.CreatorId,
                ClientId = clientId,
                ExpectedSha256 = input.Sha256
            });

        return Map(file);
    }

    /// <summary>The bytes of <paramref name="file"/>; a BLOB missing from storage is a file that is not found.</summary>
    protected virtual async Task<IRemoteStreamContent> OpenContentAsync(FileDescriptor file)
    {
        var stream = await FileDescriptorManager.GetStreamOrNullAsync(file)
                     ?? throw new EntityNotFoundException(typeof(FileDescriptor), file.Id);
        return new RemoteStreamContent(stream, file.Name, file.MimeType, file.Size);
    }

    /// <summary>
    /// Refuses a place that is not one: a container that is not managed, or a directory that is not one of the
    /// container. <c>null</c> is the container's top.
    /// </summary>
    protected virtual async Task CheckPlaceAsync(string containerName, Guid? directoryId)
    {
        GetContainer(containerName);
        if (directoryId.HasValue)
        {
            await GetDirectoryAsync(directoryId.Value, containerName);
        }
    }

    /// <summary>A file of a managed container; any other is not found.</summary>
    protected virtual async Task<FileDescriptor> GetManagedFileAsync(Guid id)
    {
        return await FindManagedFileAsync(id) ?? throw new EntityNotFoundException(typeof(FileDescriptor), id);
    }

    /// <summary>
    /// A file of a managed container, or <c>null</c>; with <paramref name="includeDeleted"/> one that is soft-deleted as
    /// well - to restore it, or to delete it for good.
    /// </summary>
    protected virtual async Task<FileDescriptor?> FindManagedFileAsync(Guid id, bool includeDeleted = false)
    {
        FileDescriptor? file;
        using (includeDeleted ? DataFilter.Disable<ISoftDelete>() : null)
        {
            file = await FileRepository.FindAsync(id);
        }

        return file != null && IsManagedContainer(file.ContainerName) ? file : null;
    }
}
