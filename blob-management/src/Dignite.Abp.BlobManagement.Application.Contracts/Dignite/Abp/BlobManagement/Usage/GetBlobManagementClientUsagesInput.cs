namespace Dignite.Abp.BlobManagement.Usage;

public class GetBlobManagementClientUsagesInput
{
    /// <summary>One container; <c>null</c> for all of them.</summary>
    public string? ContainerName { get; set; }
}
