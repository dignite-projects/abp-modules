using System;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.BlobManagement.Directories;

public class GetBlobManagementDirectoriesInput
{
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>The directory whose subdirectories are listed; <c>null</c> for the top of the container.</summary>
    public Guid? ParentId { get; set; }
}
