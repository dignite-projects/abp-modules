using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>Read links for several files at once - the files a business system's page shows.</summary>
public class CreateBlobManagementReadLinksInput
{
    public const int MaxIdCount = 100;

    [Required]
    [MaxLength(MaxIdCount)]
    public List<Guid> Ids { get; set; } = new();

    /// <summary>
    /// How long the links are valid, in seconds; <c>null</c> is <c>BlobManagementReadLinkOptions.LinkLifetime</c>, and a
    /// lifetime past <c>BlobManagementReadLinkOptions.MaxLinkLifetime</c> is cut to it.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? LifetimeInSeconds { get; set; }
}
