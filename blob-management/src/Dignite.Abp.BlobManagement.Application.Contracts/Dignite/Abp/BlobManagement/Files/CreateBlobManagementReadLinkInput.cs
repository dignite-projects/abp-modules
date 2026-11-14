using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.BlobManagement.Files;

public class CreateBlobManagementReadLinkInput
{
    /// <summary>
    /// How long the link is valid, in seconds; <c>null</c> is <c>BlobManagementReadLinkOptions.LinkLifetime</c>, and a
    /// lifetime past <c>BlobManagementReadLinkOptions.MaxLinkLifetime</c> is cut to it.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? LifetimeInSeconds { get; set; }
}
