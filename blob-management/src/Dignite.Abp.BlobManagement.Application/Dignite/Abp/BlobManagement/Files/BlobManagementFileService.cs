using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobManagement.Links;
using Dignite.Abp.BlobManagement.Uploads;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.Features;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The in-process <see cref="IBlobManagementFileService"/>: an application inside Blob Management's own host calls it,
/// a trusted caller, so it checks no permission and no client. Never exposed over HTTP
/// (<c>RemoteService(IsEnabled = false)</c>, so conventional controllers skip it); what a remote caller reaches is
/// <see cref="BlobManagementFileIntegrationService"/>, which authorizes it first. Part of the tenant's Blob Management:
/// with <see cref="BlobManagementFeatures.Enable"/> off it refuses every call.
/// </summary>
[RemoteService(IsEnabled = false)]
[RequiresFeature(BlobManagementFeatures.Enable)]
public class BlobManagementFileService : BlobManagementFileServiceBase, IBlobManagementFileService
{
    protected BlobManagementUploadTicketIssuer UploadTicketIssuer { get; }

    protected BlobManagementReadLinkIssuer ReadLinkIssuer { get; }

    public BlobManagementFileService(
        IFileDescriptorRepository fileRepository,
        FileDescriptorManager fileDescriptorManager,
        BlobManagementUploadTicketIssuer uploadTicketIssuer,
        BlobManagementReadLinkIssuer readLinkIssuer)
        : base(fileRepository, fileDescriptorManager)
    {
        UploadTicketIssuer = uploadTicketIssuer;
        ReadLinkIssuer = readLinkIssuer;
    }

    public virtual async Task<BlobManagementFileDto> GetAsync(Guid id)
    {
        return Map(await GetManagedFileAsync(id));
    }

    public virtual Task<PagedResultDto<BlobManagementFileDto>> GetListAsync(GetBlobManagementFilesInput input)
    {
        return ListFilesAsync(input);
    }

    public virtual Task<BlobManagementFileDto> CreateAsync(CreateBlobManagementFileInput input)
    {
        return StoreFileAsync(input, clientId: null);
    }

    public virtual async Task<BlobManagementFileDto> UpdateAsync(Guid id, UpdateBlobManagementFileInput input)
    {
        var file = await GetManagedFileAsync(id);
        await FileDescriptorManager.RenameAsync(file, input.Name.Trim());
        return Map(file);
    }

    public virtual async Task<BlobManagementFileDto> MoveAsync(Guid id, MoveBlobManagementFileInput input)
    {
        var file = await GetManagedFileAsync(id);
        await FileDescriptorManager.MoveAsync(file, input.DirectoryId);
        return Map(file);
    }

    public virtual async Task<BlobManagementFileDto> SetPublicAsync(Guid id, SetBlobManagementFilePublicInput input)
    {
        var file = await GetManagedFileAsync(id);
        await FileDescriptorManager.SetPublicAsync(file, input.IsPublic);
        return Map(file);
    }

    public virtual async Task<BlobManagementUploadTicketDto> CreateUploadTicketAsync(CreateBlobManagementUploadTicketInput input)
    {
        await CheckPlaceAsync(input.ContainerName, input.DirectoryId);
        return UploadTicketIssuer.Issue(input, clientId: null);
    }

    public virtual async Task<BlobManagementFileDto> ConfirmAsync(Guid id)
    {
        var file = await GetManagedFileAsync(id);
        await FileDescriptorManager.ConfirmAsync(file);
        return Map(file);
    }

    public virtual async Task<BlobManagementReadLinkDto> CreateReadLinkAsync(Guid id, CreateBlobManagementReadLinkInput input)
    {
        return await ReadLinkIssuer.IssueAsync(await GetManagedFileAsync(id), input?.LifetimeInSeconds);
    }

    public virtual async Task<ListResultDto<BlobManagementReadLinkDto>> CreateReadLinksAsync(CreateBlobManagementReadLinksInput input)
    {
        var links = new List<BlobManagementReadLinkDto>();
        foreach (var id in input.Ids.Distinct())
        {
            var file = await FindManagedFileAsync(id);
            if (file is { IsPending: false })
            {
                links.Add(await ReadLinkIssuer.IssueAsync(file, input.LifetimeInSeconds));
            }
        }

        return new ListResultDto<BlobManagementReadLinkDto>(links);
    }

    public virtual async Task DeleteAsync(Guid id)
    {
        var file = await FindManagedFileAsync(id);
        if (file != null)
        {
            await FileDescriptorManager.DeleteAsync(file);
        }
    }

    public virtual async Task<IRemoteStreamContent> GetContentAsync(Guid id)
    {
        return await OpenContentAsync(await GetManagedFileAsync(id));
    }
}
