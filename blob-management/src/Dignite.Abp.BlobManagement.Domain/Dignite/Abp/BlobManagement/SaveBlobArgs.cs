using System;

namespace Dignite.Abp.BlobManagement;

/// <summary>
/// What an application says with a BLOB it saves under a name (<see cref="FileDescriptorManager.SaveBlobAsync"/>),
/// beside the container, the name and the bytes.
/// </summary>
public class SaveBlobArgs
{
    /// <summary>
    /// Whether a BLOB already saved under the name is replaced; without it, the save fails with
    /// <see cref="BlobManagementErrorCodes.BlobAlreadyExists"/>.
    /// </summary>
    public bool OverrideExisting { get; set; }

    /// <summary>
    /// Whether anyone may read the BLOB at its public address (<see cref="FileDescriptor.IsPublic"/>): the
    /// application's container declares it, and each save says it again, so an overriding save sets it anew.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// The user the BLOB is counted for; <c>null</c> leaves it to ABP's auditing - the current user, if there is one.
    /// </summary>
    public Guid? CreatorId { get; set; }

    /// <summary>The OAuth client writing through the BLOB API, recorded on the descriptor; <c>null</c> in process.</summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// The SHA-256 (base64) the caller says the bytes have, to be verified before anything is stored; <c>null</c> asks
    /// for no check, and no hash is computed.
    /// </summary>
    public string? ExpectedSha256 { get; set; }
}
