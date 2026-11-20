using System;

namespace Dignite.Abp.BlobManagement.Usage;

public class BlobManagementTenantUsageDto
{
    /// <summary>The tenant the files belong to; <c>null</c> gathers the host's own files.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// The tenant's name, where the host has a tenant store that knows it (ABP's Tenant Management); otherwise
    /// <c>null</c>, and so for a tenant that was deleted and left files behind.
    /// </summary>
    public string? TenantName { get; set; }

    /// <summary>The sizes of the tenant's files, in every container.</summary>
    public long StoredBytes { get; set; }

    public long FileCount { get; set; }

    /// <summary>The tenant's allocation in bytes; <c>null</c> when it has no limit.</summary>
    public long? StorageSize { get; set; }
}
