using System;
using Volo.Abp.Application.Dtos;

namespace Dignite.Abp.BlobManagement.Directories;

/// <summary>A directory of a container, with what the caller may do with it and in it.</summary>
public class BlobManagementDirectoryDto : CreationAuditedEntityDto<Guid>
{
    public string ContainerName { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>The parent directory, or <c>null</c> at the top of the container.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Position among its siblings.</summary>
    public int Order { get; set; }

    public bool HasChildren { get; set; }

    /// <summary>
    /// Whether the directory has permissions of its own: it, and everything below it, is then seen only by those
    /// granted on it.
    /// </summary>
    public bool IsRestricted { get; set; }

    /// <summary>Whether the caller may rename and move the directory: the permission of the place it is in.</summary>
    public bool CanUpdate { get; set; }

    /// <summary>Whether the caller may delete the directory, once it is empty: the permission of the place it is in.</summary>
    public bool CanDelete { get; set; }

    /// <summary>What the caller may do inside the directory.</summary>
    public BlobManagementAccessDto Access { get; set; } = new();
}
