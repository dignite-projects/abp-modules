using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Abp.BlobManagement.Usage;

/// <summary>
/// What the current tenant keeps in Blob Management, for whoever holds the management permission, in one measure:
/// <b>stored bytes</b>, the sizes of the files - every file has stored bytes of its own. The tenant's are what the
/// allocation is checked against; the containers', the users' and the clients' each add up to them.
/// </summary>
public interface IBlobManagementUsageAppService : IApplicationService
{
    /// <summary>The tenant's stored bytes and allocation, and each container's stored bytes.</summary>
    Task<BlobManagementUsageDto> GetAsync();

    /// <summary>The stored bytes of each user, the largest first, a page at a time.</summary>
    Task<PagedResultDto<BlobManagementUserUsageDto>> GetUsersAsync(GetBlobManagementUserUsagesInput input);

    /// <summary>The stored bytes of each client, the largest first.</summary>
    Task<ListResultDto<BlobManagementClientUsageDto>> GetClientsAsync(GetBlobManagementClientUsagesInput input);
}
