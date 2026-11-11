using System.ComponentModel.DataAnnotations;
using Volo.Abp.Content;
using Volo.Abp.Validation;

namespace Dignite.Drive.Files;

public class CreateDriveFileInput
{
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>The file's name; <c>null</c> takes the uploaded file's own.</summary>
    [DynamicStringLength(typeof(FileDescriptorConsts), nameof(FileDescriptorConsts.MaxNameLength))]
    public string? Name { get; set; }

    [Required]
    public IRemoteStreamContent File { get; set; } = default!;
}
