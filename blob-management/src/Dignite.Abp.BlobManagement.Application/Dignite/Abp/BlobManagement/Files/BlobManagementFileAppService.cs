using System;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Blobs;
using Dignite.Abp.BlobManagement.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The file API's <see cref="IBlobManagementFileService"/>: the in-process implementation behind an authorization of
/// the caller. It admits two:
/// <list type="bullet">
/// <item>an OAuth client granted the files of the file's container, never a user (<see cref="BlobManagementClientChecker"/>) - for
/// everything; a file it stores records it as the writer. A file in a container the client may not use is not found,
/// so its id tells nothing;</item>
/// <item>a user who holds the management permission - for the reads. No user stores, renames, moves or deletes a file
/// here: that is the application's, under its own rules.</item>
/// </list>
/// Exposed only through <c>BlobManagementFileController</c>.
/// </summary>
[Authorize]
[RemoteService(IsEnabled = false)]
[ExposeServices(typeof(IBlobManagementFileAppService), typeof(BlobManagementFileAppService))]
public class BlobManagementFileAppService : BlobManagementFileService, IBlobManagementFileAppService
{
    protected BlobManagementClientChecker ClientChecker { get; }

    public BlobManagementFileAppService(
        IFileDescriptorRepository fileRepository,
        FileDescriptorManager fileDescriptorManager,
        BlobManagementClientChecker clientChecker)
        : base(fileRepository, fileDescriptorManager)
    {
        ClientChecker = clientChecker;
    }

    public override async Task<BlobManagementFileDto> GetAsync(Guid id)
    {
        return Map(await GetFileForReaderAsync(id));
    }

    public override async Task<PagedResultDto<BlobManagementFileDto>> GetListAsync(GetBlobManagementFilesInput input)
    {
        if (CurrentUser.Id != null)
        {
            await CheckManagementPermissionAsync();
        }
        else
        {
            await ClientChecker.CheckAsync(input.ContainerName);
        }

        return await base.GetListAsync(input);
    }

    public override async Task<BlobManagementFileDto> CreateAsync(CreateBlobManagementFileInput input)
    {
        var clientId = await ClientChecker.CheckAsync(input.ContainerName);
        return await CreateAsync(input, clientId);
    }

    public override async Task<BlobManagementFileDto> UpdateAsync(Guid id, UpdateBlobManagementFileInput input)
    {
        var file = await GetFileForClientAsync(id);
        await FileDescriptorManager.RenameAsync(file, input.Name.Trim());
        return Map(file);
    }

    public override async Task<BlobManagementFileDto> MoveAsync(Guid id, MoveBlobManagementFileInput input)
    {
        var file = await GetFileForClientAsync(id);
        await FileDescriptorManager.MoveAsync(file, input.DirectoryId);
        return Map(file);
    }

    public override async Task DeleteAsync(Guid id)
    {
        var file = await FindFileForClientAsync(id);
        if (file != null)
        {
            await FileDescriptorManager.DeleteAsync(file);
        }
    }

    public override async Task<IRemoteStreamContent> GetContentAsync(Guid id)
    {
        var file = await GetFileForReaderAsync(id);
        var stream = await FileDescriptorManager.GetStreamOrNullAsync(file)
                     ?? throw new EntityNotFoundException(typeof(FileDescriptor), id);
        return new RemoteStreamContent(stream, file.Name, file.MimeType, file.Size);
    }

    /// <summary>
    /// The file, for a caller who may read it: a user who holds the management permission - checked before the file is
    /// looked for - or a client granted the files of its container.
    /// </summary>
    protected virtual async Task<FileDescriptor> GetFileForReaderAsync(Guid id)
    {
        if (CurrentUser.Id != null)
        {
            await CheckManagementPermissionAsync();
            return await GetFileAsync(id);
        }

        return await GetFileForClientAsync(id);
    }

    protected virtual async Task<FileDescriptor> GetFileForClientAsync(Guid id)
    {
        return await FindFileForClientAsync(id) ?? throw new EntityNotFoundException(typeof(FileDescriptor), id);
    }

    /// <summary>
    /// The file, for the calling client; <c>null</c> when there is none or the client is not granted its container. A
    /// caller that is no client is refused before anything is looked for.
    /// </summary>
    protected virtual async Task<FileDescriptor?> FindFileForClientAsync(Guid id)
    {
        ClientChecker.CheckIsClient();
        var file = await FindFileAsync(id);
        return file != null && await ClientChecker.IsAllowedAsync(file.ContainerName) ? file : null;
    }

    protected virtual Task CheckManagementPermissionAsync()
    {
        return AuthorizationService.CheckAsync(BlobManagementPermissions.Containers.Default);
    }
}
