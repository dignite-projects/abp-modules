using System;
using Volo.Abp.Application.Dtos;

namespace Dignite.Abp.BlobManagement.Directories;

/// <summary>
/// A directory of a container. What the caller may do with it is the container's answer
/// (<c>BlobManagementContainerDto.Access</c>), the same for every directory in it.
/// </summary>
public class BlobManagementDirectoryDto : CreationAuditedEntityDto<Guid>
{
    public string ContainerName { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>The parent directory, or <c>null</c> at the top of the container.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Position among its siblings.</summary>
    public int Order { get; set; }

    public bool HasChildren { get; set; }
}
