namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The <see cref="Volo.Abp.BusinessException"/> codes the Blob Management throws; localized by <c>BlobManagementResource</c>.
/// </summary>
public static class BlobManagementErrorCodes
{
    public const string Namespace = "BlobManagement";

    /// <summary>
    /// The container is not one of Blob Management's: it was not declared with <c>UseBlobManagementContainer</c> on the Blob Management
    /// host. Data: <c>ContainerName</c>.
    /// </summary>
    public const string ContainerNotFound = Namespace + ":ContainerNotFound";

    /// <summary>
    /// The container already has a file of that name and the caller did not ask to overwrite it. Data:
    /// <c>ContainerName</c>, <c>BlobName</c>. The BLOB storing provider turns it back into ABP's
    /// <c>BlobAlreadyExistsException</c>.
    /// </summary>
    public const string BlobAlreadyExists = Namespace + ":BlobAlreadyExists";

    /// <summary>
    /// The container has no file of that name (the BLOB API's answer where the in-process service returns
    /// <c>null</c>). Data: <c>ContainerName</c>, <c>BlobName</c>.
    /// </summary>
    public const string BlobNotFound = Namespace + ":BlobNotFound";

    /// <summary>
    /// The tenant's <c>BlobManagement.Enable</c> feature is off.
    /// </summary>
    public const string BlobManagementDisabled = Namespace + ":BlobManagementDisabled";

    /// <summary>
    /// Storing the content would take the tenant past its <c>BlobManagement.StorageSize</c>. Data: <c>StorageSize</c> (the
    /// allocation in megabytes), <c>UsedBytes</c>, <c>RequiredBytes</c>.
    /// </summary>
    public const string StorageQuotaExceeded = Namespace + ":StorageQuotaExceeded";

    /// <summary>
    /// The SHA-256 the caller sent with an upload is not the SHA-256 of the bytes that arrived. Nothing was stored.
    /// Data: <c>Expected</c> and <c>Actual</c> (base64).
    /// </summary>
    public const string ChecksumMismatch = Namespace + ":ChecksumMismatch";

    /// <summary>
    /// The checksum the caller sent with an upload cannot be checked: it is not base64 of 32 bytes, or a
    /// <c>Content-Digest</c> names no SHA-256 (the only algorithm this module checks). Nothing was stored.
    /// </summary>
    public const string ChecksumInvalid = Namespace + ":ChecksumInvalid";

    /// <summary>
    /// The container does not allow directories (<c>AllowDirectories</c>, off by default). Data: <c>ContainerName</c>.
    /// </summary>
    public const string DirectoriesNotAllowed = Namespace + ":DirectoriesNotAllowed";

    /// <summary>The directory does not exist, or is in another container.</summary>
    public const string DirectoryNotFound = Namespace + ":DirectoryNotFound";

    /// <summary>A live sibling already has the name, whatever its case. Data: <c>Name</c>.</summary>
    public const string DirectoryNameAlreadyExists = Namespace + ":DirectoryNameAlreadyExists";

    public const string DirectoryCannotMoveIntoItself = Namespace + ":DirectoryCannotMoveIntoItself";

    /// <summary>Data: <c>MaxDepth</c>.</summary>
    public const string DirectoryTooDeep = Namespace + ":DirectoryTooDeep";

    /// <summary>The directory still has a live subdirectory or file.</summary>
    public const string DirectoryNotEmpty = Namespace + ":DirectoryNotEmpty";

    /// <summary>Data: <c>Sorting</c>.</summary>
    public const string SortingNotSupported = Namespace + ":SortingNotSupported";

    /// <summary>
    /// The file was saved by an application under its name, which is how the application reads it back: it is not
    /// renamed or moved. Data: <c>Name</c>.
    /// </summary>
    public const string FileAddressedByName = Namespace + ":FileAddressedByName";

    /// <summary>An upload ticket that is expired, altered, or was not issued by this Blob Management.</summary>
    public const string UploadTicketInvalid = Namespace + ":UploadTicketInvalid";

    /// <summary>An upload ticket whose file was stored already: a ticket stores one file, once.</summary>
    public const string UploadTicketUsed = Namespace + ":UploadTicketUsed";

    /// <summary>A pending file - uploaded with a ticket, not confirmed yet - asked for what only a confirmed one has.</summary>
    public const string FilePending = Namespace + ":FilePending";

    /// <summary>A read link that is expired, altered, or was not issued by this Blob Management.</summary>
    public const string ReadLinkInvalid = Namespace + ":ReadLinkInvalid";

    /// <summary>
    /// An upload asked for a size for its pictures in a container that crops, stretches or pads them: only a container
    /// that scales its pictures to fit, or resizes none, takes one.
    /// </summary>
    public const string ImageSizeNotSupported = Namespace + ":ImageSizeNotSupported";
}
