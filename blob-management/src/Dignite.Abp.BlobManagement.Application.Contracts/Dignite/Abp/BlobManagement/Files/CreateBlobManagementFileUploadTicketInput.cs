using System;
using System.ComponentModel.DataAnnotations;
using Dignite.Abp.BlobManagement.Tickets;
using Volo.Abp.Validation;

namespace Dignite.Abp.BlobManagement.Files;

public class CreateBlobManagementFileUploadTicketInput
{
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>The directory the file is filed in; <c>null</c> for the top of the container.</summary>
    public Guid? DirectoryId { get; set; }

    /// <summary>The name the file gets; whatever the upload calls it is ignored.</summary>
    [Required]
    [DynamicStringLength(typeof(FileDescriptorConsts), nameof(FileDescriptorConsts.MaxNameLength))]
    public string FileName { get; set; } = default!;

    /// <summary>
    /// How long the ticket lives, in seconds; at most <see cref="BlobManagementTicketConsts.MaxLifetimeSeconds"/>. <c>null</c>:
    /// <see cref="BlobManagementTicketConsts.DefaultLifetimeSeconds"/>.
    /// </summary>
    [Range(1, BlobManagementTicketConsts.MaxLifetimeSeconds)]
    public int? LifetimeSeconds { get; set; }
}
