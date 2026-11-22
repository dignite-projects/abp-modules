using System;
using System.Collections.Generic;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// What an application says with a file it keeps by id (<see cref="FileDescriptorManager.CreateFileAsync"/>), beside
/// the container, the name and the bytes.
/// </summary>
public class CreateFileArgs
{
    /// <summary>
    /// The id the file takes; <c>null</c> generates one. An upload ticket names its file's id in advance, so a second
    /// upload with it collides with the first one's file.
    /// </summary>
    public Guid? Id { get; set; }

    /// <summary>
    /// The directory the file is filed in, one of its container; <c>null</c> is the container's top.
    /// </summary>
    public Guid? DirectoryId { get; set; }

    /// <summary>Whether anyone may read the file at its public address (<see cref="FileDescriptor.IsPublic"/>).</summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// Whether the file waits for its business system to confirm it (<see cref="FileDescriptor.IsPending"/>): one that
    /// came in through an upload ticket.
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// The user the file is counted for; <c>null</c> leaves it to ABP's auditing - the current user, if there is one.
    /// </summary>
    public Guid? CreatorId { get; set; }

    /// <summary>The OAuth client writing through the file API, recorded on the descriptor; <c>null</c> in process.</summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// The SHA-256 (base64) the caller says the bytes have, to be verified before anything is stored; <c>null</c> asks
    /// for no check, and no hash is computed.
    /// </summary>
    public string? ExpectedSha256 { get; set; }

    /// <summary>
    /// Fields of its own the application keeps on the file - the descriptor's extra properties - written with the row,
    /// so it is inserted once: the path a file had in the library an application migrates in, for one, which
    /// <c>DirectoryPathImporter</c> files it by. Blob Management reads none of them itself.
    /// </summary>
    public Dictionary<string, object?> ExtraProperties { get; } = new();
}
