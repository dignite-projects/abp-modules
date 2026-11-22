using System;
using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Features;
using Dignite.Abp.BlobManagement.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;
using Volo.Abp.Features;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The files of the managed containers, for a user who holds the management permission: the management page lists a
/// place, shows one file and downloads its bytes. It reads and changes nothing - no user stores, renames, moves or
/// deletes a file through the module: that is its business system's, through <see cref="IBlobManagementFileService"/>.
/// A token without a user - a client acting for itself - is refused, as the other services for users refuse it: a
/// client keeps files through the integration service. Part of the tenant's Blob Management: with
/// <see cref="BlobManagementFeatures.Enable"/> off it refuses every call.
/// </summary>
[Authorize(BlobManagementPermissions.Containers.Default)]
[RequiresFeature(BlobManagementFeatures.Enable)]
public class BlobManagementFileAppService : BlobManagementFileServiceBase, IBlobManagementFileAppService
{
    public BlobManagementFileAppService(
        IFileDescriptorRepository fileRepository,
        FileDescriptorManager fileDescriptorManager)
        : base(fileRepository, fileDescriptorManager)
    {
    }

    public virtual async Task<BlobManagementFileDto> GetAsync(Guid id)
    {
        GetUserId();
        return Map(await GetManagedFileAsync(id));
    }

    public virtual async Task<PagedResultDto<BlobManagementFileDto>> GetListAsync(GetBlobManagementFilesInput input)
    {
        GetUserId();
        return await ListFilesAsync(input);
    }

    public virtual async Task<IRemoteStreamContent> GetContentAsync(Guid id)
    {
        GetUserId();
        return await OpenContentAsync(await GetManagedFileAsync(id));
    }
}
