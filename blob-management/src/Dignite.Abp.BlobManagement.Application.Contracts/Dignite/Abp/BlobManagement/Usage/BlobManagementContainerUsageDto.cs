namespace Dignite.Abp.BlobManagement.Usage;

public class BlobManagementContainerUsageDto
{
    public string ContainerName { get; set; } = default!;

    /// <summary>The bytes the container stores: the sizes of its files.</summary>
    public long StoredBytes { get; set; }

    public long FileCount { get; set; }
}
