using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// The files of one place of a container. <c>Sorting</c> is one of <c>Name</c>, <c>Size</c>, <c>MimeType</c>,
/// <c>CreationTime</c> or <c>LastModificationTime</c>, with <c>asc</c> or <c>desc</c>; by default the name.
/// </summary>
public class GetBlobManagementFilesInput : PagedAndSortedResultRequestDto
{
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>The directory; <c>null</c> for the top of the container.</summary>
    public Guid? DirectoryId { get; set; }

    /// <summary>A part of the name, whatever its case.</summary>
    public string? Filter { get; set; }
}
