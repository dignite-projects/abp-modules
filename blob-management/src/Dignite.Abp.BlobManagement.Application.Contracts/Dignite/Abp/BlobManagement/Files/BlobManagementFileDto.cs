using System;
using Volo.Abp.Application.Dtos;

namespace Dignite.Abp.BlobManagement.Files;

/// <summary>
/// A file of a managed container. Extensible: what an application adds to a file travels in <c>ExtraProperties</c>.
/// </summary>
public class BlobManagementFileDto : ExtensibleCreationAuditedEntityDto<Guid>
{
    public string ContainerName { get; set; } = default!;

    /// <summary>The directory the file is filed in, or <c>null</c> at the top of its container.</summary>
    public Guid? DirectoryId { get; set; }

    /// <summary>
    /// The name of an uploaded file is a display name and may repeat; that of a file an application saved is its
    /// address (<see cref="IsAddressedByName"/>).
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// Whether an application saved the file under its name and reads it back by it. Such a file is not renamed or
    /// moved.
    /// </summary>
    public bool IsAddressedByName { get; set; }

    /// <summary>Detected from the stored bytes.</summary>
    public string MimeType { get; set; } = default!;

    /// <summary>The size of the stored bytes.</summary>
    public long Size { get; set; }

    /// <summary>The OAuth client that stored the file over HTTP; <c>null</c> when an application stored it in process.</summary>
    public string? ClientId { get; set; }

    public DateTime? LastModificationTime { get; set; }

    /// <summary>
    /// Whether anyone may read the file at its public address (<see cref="Url"/>). A file saved under a name takes it
    /// from its application's container at each save; a file kept by id takes it when it is stored, and changes it with
    /// <see cref="IBlobManagementFileService.SetPublicAsync"/>.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// For a public file, the address anyone can read it at, relative to the host that serves Blob Management
    /// (<c>/blobs/{container}/{blob}</c>, with <c>?__tenant=</c> for a tenant's file); otherwise <c>null</c> - the
    /// application reads it and hands its bytes on, or asks for a read link.
    /// </summary>
    public string? Url { get; set; }
}
