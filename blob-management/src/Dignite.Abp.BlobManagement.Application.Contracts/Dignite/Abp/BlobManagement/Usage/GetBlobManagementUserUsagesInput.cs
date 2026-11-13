using Volo.Abp.Application.Dtos;

namespace Dignite.Abp.BlobManagement.Usage;

public class GetBlobManagementUserUsagesInput : PagedResultRequestDto
{
    /// <summary>One container; <c>null</c> for all of them.</summary>
    public string? ContainerName { get; set; }
}
