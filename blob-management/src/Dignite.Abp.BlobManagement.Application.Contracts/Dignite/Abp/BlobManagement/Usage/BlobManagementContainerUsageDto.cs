namespace Dignite.Abp.BlobManagement.Usage;

public class BlobManagementContainerUsageDto
{
    public string ContainerName { get; set; } = default!;

    /// <summary>The bytes the container stores: each physical BLOB once.</summary>
    public long StoredBytes { get; set; }

    public long FileCount { get; set; }
}
