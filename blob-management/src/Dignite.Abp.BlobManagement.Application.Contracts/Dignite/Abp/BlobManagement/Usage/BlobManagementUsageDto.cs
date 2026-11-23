using System.Collections.Generic;

namespace Dignite.Abp.BlobManagement.Usage;

public class BlobManagementUsageDto
{
    /// <summary>
    /// The bytes the tenant stores: the sizes of its files, the soft-deleted ones included - they keep their bytes until
    /// their business system deletes them for good.
    /// </summary>
    public long StoredBytes { get; set; }

    /// <summary>The tenant's allocation in bytes; <c>null</c> when it has no limit.</summary>
    public long? StorageSize { get; set; }

    /// <summary>The tenant's files, the soft-deleted ones included.</summary>
    public long FileCount { get; set; }

    /// <summary>The part of <see cref="StoredBytes"/> that soft-deleted files keep.</summary>
    public long DeletedBytes { get; set; }

    /// <summary>The part of <see cref="FileCount"/> that is soft-deleted.</summary>
    public long DeletedFileCount { get; set; }

    /// <summary>
    /// Every managed container, by name, and any other that still holds files. Their stored bytes add up to the
    /// tenant's.
    /// </summary>
    public List<BlobManagementContainerUsageDto> Containers { get; set; } = new();
}
