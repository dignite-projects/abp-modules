using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace Dignite.Abp.BlobManagement.Usage;

/// <summary>What the current tenant keeps in Blob Management: <c>/api/blob-management/usage</c>.</summary>
[Area(BlobManagementRemoteServiceConsts.ModuleName)]
[RemoteService(Name = BlobManagementRemoteServiceConsts.RemoteServiceName)]
[Route("api/blob-management/usage")]
public class BlobManagementUsageController : BlobManagementController, IBlobManagementUsageAppService
{
    protected IBlobManagementUsageAppService UsageAppService { get; }

    public BlobManagementUsageController(IBlobManagementUsageAppService usageAppService)
    {
        UsageAppService = usageAppService;
    }

    [HttpGet]
    public virtual Task<BlobManagementUsageDto> GetAsync()
    {
        return UsageAppService.GetAsync();
    }

    [HttpGet]
    [Route("users")]
    public virtual Task<PagedResultDto<BlobManagementUserUsageDto>> GetUsersAsync(GetBlobManagementUserUsagesInput input)
    {
        return UsageAppService.GetUsersAsync(input);
    }

    [HttpGet]
    [Route("clients")]
    public virtual Task<ListResultDto<BlobManagementClientUsageDto>> GetClientsAsync(GetBlobManagementClientUsagesInput input)
    {
        return UsageAppService.GetClientsAsync(input);
    }
}
