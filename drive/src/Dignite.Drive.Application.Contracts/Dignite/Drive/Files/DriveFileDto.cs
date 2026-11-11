using System;
using Volo.Abp.Application.Dtos;

namespace Dignite.Drive.Files;

/// <summary>
/// A file of a user drive. Extensible: what a module building on the Drive adds to a file travels in
/// <c>ExtraProperties</c>.
/// </summary>
public class DriveFileDto : ExtensibleCreationAuditedEntityDto<Guid>
{
    public string ContainerName { get; set; } = default!;

    /// <summary>Its display name; names may repeat.</summary>
    public string Name { get; set; } = default!;

    /// <summary>Detected from the stored bytes.</summary>
    public string MimeType { get; set; } = default!;

    /// <summary>The size of the stored bytes.</summary>
    public long Size { get; set; }

    public DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// In a <c>PublicRead</c> container, the address anyone can read it at, relative to the host that serves the Drive
    /// (<c>/api/drive/public/{container}/{blob}</c>, with <c>?__tenant=</c> for a tenant's file); otherwise <c>null</c> -
    /// the file is read through the file API.
    /// </summary>
    public string? Url { get; set; }
}
