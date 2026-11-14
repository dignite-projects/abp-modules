using System.Threading.Tasks;
using Dignite.Abp.BlobManagement.Public;
using Volo.Abp.Application.Services;

namespace Dignite.Abp.BlobManagement.Links;

/// <summary>
/// A browser's read with a link (<see cref="Files.IBlobManagementFileService.CreateReadLinkAsync"/>): anonymous - the
/// link is the credential - in the tenant the link names. Behind <c>/api/blob-management/links/{link}</c>; a business
/// system never calls it.
/// </summary>
public interface IBlobManagementReadLinkAppService : IApplicationService
{
    /// <summary>
    /// Reads the file the link names once, as a public read does (<see cref="IBlobManagementPublicAppService.ReadAsync"/>):
    /// its information and entity tag, whether the client's copy is current, and - unless it is, or
    /// <paramref name="request"/> wants headers only - its bytes. An expired, altered or foreign link is refused with
    /// <see cref="BlobManagementErrorCodes.ReadLinkInvalid"/>; a file that is gone is
    /// <see cref="Volo.Abp.Domain.Entities.EntityNotFoundException"/>.
    /// </summary>
    Task<BlobManagementReadLinkResult> ReadAsync(string link, BlobManagementPublicReadRequest request);
}
