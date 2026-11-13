using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace Dignite.Abp.BlobManagement.Directories;

public class CreateBlobManagementDirectoryInput
{
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>The directory to create it in; <c>null</c> for the top of the container.</summary>
    public Guid? ParentId { get; set; }

    [Required]
    [DynamicStringLength(typeof(DirectoryDescriptorConsts), nameof(DirectoryDescriptorConsts.MaxNameLength))]
    public string Name { get; set; } = default!;
}
