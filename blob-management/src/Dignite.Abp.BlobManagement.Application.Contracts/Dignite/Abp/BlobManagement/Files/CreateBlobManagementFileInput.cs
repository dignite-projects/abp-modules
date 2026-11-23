using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Content;
using Volo.Abp.Validation;

namespace Dignite.Abp.BlobManagement.Files;

public class CreateBlobManagementFileInput
{
    [Required]
    public string ContainerName { get; set; } = default!;

    /// <summary>The directory to file it in; <c>null</c> for the top of the container.</summary>
    public Guid? DirectoryId { get; set; }

    /// <summary>The file's name; <c>null</c> takes the uploaded file's own.</summary>
    [DynamicStringLength(typeof(FileDescriptorConsts), nameof(FileDescriptorConsts.MaxNameLength))]
    public string? Name { get; set; }

    /// <summary>
    /// The user the upload is counted for in the usage figures; no right comes with it. <c>null</c>: the application's
    /// current user - in process as ABP's auditing records it, over HTTP as
    /// <c>Dignite.Abp.BlobManagement.HttpApi.Client</c> sends it, since the client's token carries no user.
    /// </summary>
    public Guid? CreatorId { get; set; }

    /// <summary>
    /// Whether anyone may read the file at its public address, without signing in; the application's call. Changed
    /// later with <see cref="IBlobManagementFileService.SetPublicAsync"/>.
    /// </summary>
    public bool IsPublic { get; set; }

    /// <summary>
    /// The SHA-256 of the file as base64, when the caller wants the upload verified: a different one fails with
    /// <see cref="BlobManagementErrorCodes.ChecksumMismatch"/> and nothing is stored. <c>null</c> asks for no check, and
    /// no hash is computed.
    /// </summary>
    [DynamicStringLength(typeof(FileDescriptorConsts), nameof(FileDescriptorConsts.MaxHashLength))]
    public string? Sha256 { get; set; }

    /// <summary>
    /// The largest width a picture is stored at, in pixels; <c>null</c> leaves it to the container. A wider picture is
    /// scaled down, keeping its aspect ratio - never cropped, never enlarged - and the container's own box still applies:
    /// each side is the smaller of the two. Content that is not a picture is stored as it is.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? ImageMaxWidth { get; set; }

    /// <summary>The largest height a picture is stored at, in pixels; <c>null</c> leaves it to the container.</summary>
    [Range(1, int.MaxValue)]
    public int? ImageMaxHeight { get; set; }

    [Required]
    public IRemoteStreamContent File { get; set; } = default!;
}
