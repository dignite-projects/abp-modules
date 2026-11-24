using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Blobs;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobManagement.Links;
using Dignite.Abp.BlobManagement.Uploads;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Features;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The file API's <see cref="IBlobManagementFileService"/>, an ABP integration service: every call authorizes its
/// caller first - an OAuth client granted the files of the file's container, never a user
/// (<see cref="BlobManagementClientChecker"/>) - and a file it stores records the client as its writer. A file in a
/// container the client may not use is not found, so its id tells nothing. No user is admitted here: what a user reads
/// at the management page is <see cref="BlobManagementFileAppService"/>. Part of the tenant's Blob Management: with
/// <see cref="BlobManagementFeatures.Enable"/> off it refuses every call.
/// <para>
/// Exposed only through <c>BlobManagementFileIntegrationController</c>, and only where the host exposes integration
/// services.
/// </para>
/// </summary>
[Authorize]
[RemoteService(IsEnabled = false)]
[RequiresFeature(BlobManagementFeatures.Enable)]
[ExposeServices(typeof(IBlobManagementFileIntegrationService), typeof(BlobManagementFileIntegrationService))]
public class BlobManagementFileIntegrationService : BlobManagementFileServiceBase, IBlobManagementFileIntegrationService
{
    protected BlobManagementUploadTicketIssuer UploadTicketIssuer { get; }

    protected BlobManagementReadLinkIssuer ReadLinkIssuer { get; }

    protected BlobManagementClientChecker ClientChecker { get; }

    public BlobManagementFileIntegrationService(
        IFileDescriptorRepository fileRepository,
        FileDescriptorManager fileDescriptorManager,
        BlobManagementUploadTicketIssuer uploadTicketIssuer,
        BlobManagementReadLinkIssuer readLinkIssuer,
        BlobManagementClientChecker clientChecker)
        : base(fileRepository, fileDescriptorManager)
    {
        UploadTicketIssuer = uploadTicketIssuer;
        ReadLinkIssuer = readLinkIssuer;
        ClientChecker = clientChecker;
    }

    public virtual async Task<BlobManagementFileDto> GetAsync(Guid id)
    {
        return Map(await GetFileForClientAsync(id));
    }

    public virtual async Task<PagedResultDto<BlobManagementFileDto>> GetListAsync(GetBlobManagementFilesInput input)
    {
        await ClientChecker.CheckAsync(input.ContainerName);
        return await ListFilesAsync(input);
    }

    public virtual async Task<BlobManagementFileDto> CreateAsync(CreateBlobManagementFileInput input)
    {
        var clientId = await ClientChecker.CheckAsync(input.ContainerName);
        return await StoreFileAsync(input, clientId);
    }

    public virtual async Task<BlobManagementFileDto> UpdateAsync(Guid id, UpdateBlobManagementFileInput input)
    {
        var file = await GetFileForClientAsync(id);
        await FileDescriptorManager.RenameAsync(file, input.Name.Trim());
        return Map(file);
    }

    public virtual async Task<BlobManagementFileDto> MoveAsync(Guid id, MoveBlobManagementFileInput input)
    {
        var file = await GetFileForClientAsync(id);
        await FileDescriptorManager.MoveAsync(file, input.DirectoryId);
        return Map(file);
    }

    public virtual async Task<BlobManagementFileDto> SetPublicAsync(Guid id, SetBlobManagementFilePublicInput input)
    {
        var file = await GetFileForClientAsync(id);
        await FileDescriptorManager.SetPublicAsync(file, input.IsPublic);
        return Map(file);
    }

    public virtual async Task<BlobManagementUploadTicketDto> CreateUploadTicketAsync(CreateBlobManagementUploadTicketInput input)
    {
        var clientId = await ClientChecker.CheckAsync(input.ContainerName);
        await CheckPlaceAsync(input.ContainerName, input.DirectoryId);
        return UploadTicketIssuer.Issue(input, clientId);
    }

    public virtual async Task<BlobManagementFileDto> ConfirmAsync(Guid id)
    {
        var file = await GetFileForClientAsync(id);
        await FileDescriptorManager.ConfirmAsync(file);
        return Map(file);
    }

    public virtual async Task<BlobManagementReadLinkDto> CreateReadLinkAsync(Guid id, CreateBlobManagementReadLinkInput input)
    {
        return await ReadLinkIssuer.IssueAsync(await GetFileForClientAsync(id), input.LifetimeInSeconds);
    }

    public virtual async Task<ListResultDto<BlobManagementReadLinkDto>> CreateReadLinksAsync(CreateBlobManagementReadLinksInput input)
    {
        // Before anything is looked for, and for an empty list as well: a caller that is no client gets nothing here.
        ClientChecker.CheckIsClient();

        var links = new List<BlobManagementReadLinkDto>();
        foreach (var id in input.Ids.Distinct())
        {
            var file = await FindFileForClientAsync(id);
            if (file is { IsPending: false })
            {
                links.Add(await ReadLinkIssuer.IssueAsync(file, input.LifetimeInSeconds));
            }
        }

        return new ListResultDto<BlobManagementReadLinkDto>(links);
    }

    public virtual async Task DeleteAsync(Guid id, bool permanently = false)
    {
        var file = await FindFileForClientAsync(id, includeDeleted: permanently);
        if (file != null)
        {
            await FileDescriptorManager.DeleteAsync(file, permanently);
        }
    }

    public virtual async Task<BlobManagementFileDto> RestoreAsync(Guid id)
    {
        var file = await FindFileForClientAsync(id, includeDeleted: true)
                   ?? throw new EntityNotFoundException(typeof(FileDescriptor), id);
        return Map(await FileDescriptorManager.RestoreAsync(file));
    }

    public virtual async Task<IRemoteStreamContent> GetContentAsync(Guid id)
    {
        return await OpenContentAsync(await GetFileForClientAsync(id));
    }

    protected virtual async Task<FileDescriptor> GetFileForClientAsync(Guid id)
    {
        return await FindFileForClientAsync(id) ?? throw new EntityNotFoundException(typeof(FileDescriptor), id);
    }

    /// <summary>
    /// The file, for the calling client; <c>null</c> when there is none or the client is not granted its container. A
    /// caller that is no client is refused before anything is looked for. With <paramref name="includeDeleted"/> a
    /// soft-deleted file is found too.
    /// </summary>
    protected virtual async Task<FileDescriptor?> FindFileForClientAsync(Guid id, bool includeDeleted = false)
    {
        ClientChecker.CheckIsClient();
        var file = await FindManagedFileAsync(id, includeDeleted);
        return file != null && await ClientChecker.IsAllowedAsync(file.ContainerName) ? file : null;
    }
}
