using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.BlobManagement.Tickets;

public class CreateBlobManagementTicketInput
{
    /// <summary>The Blob Management system container.</summary>
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>The caller's BLOB name the browser stores or reads.</summary>
    [Required]
    public string BlobName { get; set; } = default!;

    public BlobManagementTicketKind Kind { get; set; }

    /// <summary>
    /// Upload only: the largest file the browser may send, in bytes - below the container's own cap, which applies
    /// anyway. <c>null</c>: the container's cap.
    /// </summary>
    [Range(1, long.MaxValue)]
    public long? MaxSizeInBytes { get; set; }

    /// <summary>Upload only: whether the browser may replace an existing file of that name.</summary>
    public bool OverrideExisting { get; set; }

    /// <summary>
    /// How long the ticket lives, in seconds; at most <see cref="BlobManagementTicketConsts.MaxLifetimeSeconds"/>. <c>null</c>:
    /// <see cref="BlobManagementTicketConsts.DefaultLifetimeSeconds"/>.
    /// </summary>
    [Range(1, BlobManagementTicketConsts.MaxLifetimeSeconds)]
    public int? LifetimeSeconds { get; set; }
}
