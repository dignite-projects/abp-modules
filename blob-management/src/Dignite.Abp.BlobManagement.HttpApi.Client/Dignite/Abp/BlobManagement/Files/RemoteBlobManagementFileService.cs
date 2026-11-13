using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The remote <see cref="IBlobManagementFileService"/>: the generated <see cref="BlobManagementFileClientProxy"/>,
/// keeping the contract the in-process service keeps. The caller authenticates as an OAuth client (see
/// <c>RemoteBlobManagementBlobService</c>), whose token carries no user: a file it stores names the application's
/// current user - the one it is counted for - unless the caller named another, as the in-process service takes it from
/// ABP's auditing.
/// </summary>
[DisableConventionalRegistration]
public class RemoteBlobManagementFileService : IBlobManagementFileService
{
    protected BlobManagementFileClientProxy Proxy { get; }

    protected ICurrentUser CurrentUser { get; }

    public RemoteBlobManagementFileService(BlobManagementFileClientProxy proxy, ICurrentUser currentUser)
    {
        Proxy = proxy;
        CurrentUser = currentUser;
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
        input.CreatorId ??= CurrentUser.Id;
        return Proxy.CreateAsync(input);
    }

    public virtual Task<BlobManagementFileDto> UpdateAsync(Guid id, UpdateBlobManagementFileInput input)
    {
        return Proxy.UpdateAsync(id, input);
    }

    public virtual Task<BlobManagementFileDto> MoveAsync(Guid id, MoveBlobManagementFileInput input)
    {
        return Proxy.MoveAsync(id, input);
    }

    public virtual Task DeleteAsync(Guid id)
    {
        return Proxy.DeleteAsync(id);
    }

    public virtual Task<IRemoteStreamContent> GetContentAsync(Guid id)
    {
        return Proxy.GetContentAsync(id);
    }
}
