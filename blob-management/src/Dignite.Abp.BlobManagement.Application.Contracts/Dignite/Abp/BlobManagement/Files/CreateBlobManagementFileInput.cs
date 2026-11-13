using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Content;
using Volo.Abp.Validation;

namespace Dignite.Abp.BlobManagement.Files;

public class CreateBlobManagementFileInput
{
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>The directory to file it in; <c>null</c> for the top of the container.</summary>
    public Guid? DirectoryId { get; set; }

    /// <summary>The file's name; <c>null</c> takes the uploaded file's own.</summary>
    [DynamicStringLength(typeof(FileDescriptorConsts), nameof(FileDescriptorConsts.MaxNameLength))]
    public string? Name { get; set; }

    [Required]
    public IRemoteStreamContent File { get; set; } = default!;
}
