using System.ComponentModel.DataAnnotations;
using Dignite.Drive.Tickets;
using Volo.Abp.Validation;

namespace Dignite.Drive.Files;

public class CreateDriveFileUploadTicketInput
{
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>The name the file gets; whatever the upload calls it is ignored.</summary>
    [Required]
    [DynamicStringLength(typeof(FileDescriptorConsts), nameof(FileDescriptorConsts.MaxNameLength))]
    public string FileName { get; set; } = default!;

    /// <summary>
    /// How long the ticket lives, in seconds; at most <see cref="DriveTicketConsts.MaxLifetimeSeconds"/>. <c>null</c>:
    /// <see cref="DriveTicketConsts.DefaultLifetimeSeconds"/>.
    /// </summary>
    [Range(1, DriveTicketConsts.MaxLifetimeSeconds)]
    public int? LifetimeSeconds { get; set; }
}
