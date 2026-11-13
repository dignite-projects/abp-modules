using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Dignite.Abp.BlobManagement.Usage;

/// <summary>
/// What the current tenant keeps in Blob Management, for whoever holds the management permission.
/// <para>
/// Two measures, which do not add up to each other. <b>Stored bytes</b> - the tenant's and each container's - count
/// every physical BLOB once, however many files share it: what the allocation is checked against. <b>Uploaded
/// bytes</b> - each user's and each client's - are the sizes of their files: two users who uploaded the same bytes are
/// each counted for them.
/// </para>
/// </summary>
public interface IBlobManagementUsageAppService : IApplicationService
{
    /// <summary>The tenant's stored bytes and allocation, and each container's stored bytes.</summary>
    Task<BlobManagementUsageDto> GetAsync();

    /// <summary>The uploaded bytes of each user, the largest first, a page at a time.</summary>
    Task<PagedResultDto<BlobManagementUserUsageDto>> GetUsersAsync(GetBlobManagementUserUsagesInput input);

    /// <summary>The uploaded bytes of each client, the largest first.</summary>
    Task<ListResultDto<BlobManagementClientUsageDto>> GetClientsAsync(GetBlobManagementClientUsagesInput input);
}
