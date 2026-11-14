using System;

namespace Dignite.Abp.BlobManagement;

/// <summary>What one container stores: the sizes of its files.</summary>
public class BlobManagementContainerUsage
{
    public string ContainerName { get; set; } = default!;

    public long StoredBytes { get; set; }

    public long FileCount { get; set; }
}

/// <summary>What is counted for one user: the sizes of the files recorded for them.</summary>
public class BlobManagementCreatorUsage
{
    /// <summary><c>null</c> gathers the files counted for no user.</summary>
    public Guid? CreatorId { get; set; }

    public long StoredBytes { get; set; }

    public long FileCount { get; set; }
}

/// <summary>What one client stored over HTTP: the sizes of its files.</summary>
public class BlobManagementClientUsage
{
    /// <summary><c>null</c> gathers the files an application stored in process.</summary>
    public string? ClientId { get; set; }

    public long StoredBytes { get; set; }

    public long FileCount { get; set; }
}
