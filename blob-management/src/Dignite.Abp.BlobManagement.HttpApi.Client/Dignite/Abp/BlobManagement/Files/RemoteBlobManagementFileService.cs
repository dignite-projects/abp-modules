using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The remote <see cref="IBlobManagementFileService"/>: the generated <see cref="BlobManagementFileIntegrationClientProxy"/>,
/// keeping the contract the in-process service keeps. The caller authenticates as an OAuth client (see
/// <c>RemoteBlobManagementBlobService</c>), whose token carries no user, so the user the in-process service would take
/// from the application is sent along when the caller names none:
/// <list type="bullet">
/// <item>a stored file is counted for the current user when that user belongs to the current tenant, and for none
/// otherwise - the rule of ABP's auditing, which records the creator in process (<see cref="GetCreatorId"/>);</item>
/// <item>an upload ticket is for the current user, whatever their tenant - the identity the business system checks
/// before it confirms the file, as the in-process service takes it from <c>ICurrentUser</c>.</item>
/// </list>
/// The caller's input is never changed: what is sent is a copy.
/// </summary>
[DisableConventionalRegistration]
public class RemoteBlobManagementFileService : IBlobManagementFileService
{
    protected BlobManagementFileIntegrationClientProxy Proxy { get; }

    protected ICurrentUser CurrentUser { get; }

    protected ICurrentTenant CurrentTenant { get; }

    public RemoteBlobManagementFileService(
        BlobManagementFileIntegrationClientProxy proxy,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant)
    {
        Proxy = proxy;
        CurrentUser = currentUser;
        CurrentTenant = currentTenant;
    }

    public virtual Task<BlobManagementFileDto> GetAsync(Guid id)
    {
        return Proxy.GetAsync(id);
    }

    public virtual Task<PagedResultDto<BlobManagementFileDto>> GetListAsync(GetBlobManagementFilesInput input)
    {
        return Proxy.GetListAsync(input);
    }

    public virtual Task<BlobManagementFileDto> CreateAsync(CreateBlobManagementFileInput input)
    {
        // A copy of the fields; the content is the caller's stream, passed on as it is.
        return Proxy.CreateAsync(new CreateBlobManagementFileInput
        {
            ContainerName = input.ContainerName,
            DirectoryId = input.DirectoryId,
            Name = input.Name,
            CreatorId = input.CreatorId ?? GetCreatorId(),
            IsPublic = input.IsPublic,
            Sha256 = input.Sha256,
            File = input.File
        });
    }

    public virtual Task<BlobManagementFileDto> UpdateAsync(Guid id, UpdateBlobManagementFileInput input)
    {
        return Proxy.UpdateAsync(id, input);
    }

    public virtual Task<BlobManagementFileDto> MoveAsync(Guid id, MoveBlobManagementFileInput input)
    {
        return Proxy.MoveAsync(id, input);
    }

    public virtual Task<BlobManagementFileDto> SetPublicAsync(Guid id, SetBlobManagementFilePublicInput input)
    {
        return Proxy.SetPublicAsync(id, input);
    }

    public virtual Task<BlobManagementUploadTicketDto> CreateUploadTicketAsync(CreateBlobManagementUploadTicketInput input)
    {
        return Proxy.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput
        {
            ContainerName = input.ContainerName,
            DirectoryId = input.DirectoryId,
            UploaderId = input.UploaderId ?? CurrentUser.Id,
            MaxSize = input.MaxSize,
            AllowedContentTypes = new List<string>(input.AllowedContentTypes),
            LifetimeInSeconds = input.LifetimeInSeconds
        });
    }

    public virtual Task<BlobManagementFileDto> ConfirmAsync(Guid id)
    {
        return Proxy.ConfirmAsync(id);
    }

    public virtual Task<BlobManagementReadLinkDto> CreateReadLinkAsync(Guid id, CreateBlobManagementReadLinkInput input)
    {
        return Proxy.CreateReadLinkAsync(id, input);
    }

    public virtual Task<ListResultDto<BlobManagementReadLinkDto>> CreateReadLinksAsync(CreateBlobManagementReadLinksInput input)
    {
        return Proxy.CreateReadLinksAsync(input);
    }

    public virtual Task DeleteAsync(Guid id)
    {
        return Proxy.DeleteAsync(id);
    }

    public virtual Task<IRemoteStreamContent> GetContentAsync(Guid id)
    {
        return Proxy.GetContentAsync(id);
    }

    /// <summary>
    /// The user a file stored without a named one is counted for: the current user, when they belong to the current
    /// tenant (ABP's <c>AuditPropertySetter.SetCreatorId</c>), and <c>null</c> otherwise.
    /// </summary>
    protected virtual Guid? GetCreatorId()
    {
        return CurrentUser.TenantId == CurrentTenant.Id ? CurrentUser.Id : null;
    }
}
