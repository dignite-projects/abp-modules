using System;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.BlobManagement.Directories;

public class MoveBlobManagementDirectoryInput
{
    /// <summary>The new parent, of the same container; <c>null</c> for the top of the container.</summary>
    public Guid? ParentId { get; set; }

    /// <summary>The position among its new siblings; those at or after it move down by one.</summary>
    [Range(0, int.MaxValue)]
    public int Order { get; set; }
}
