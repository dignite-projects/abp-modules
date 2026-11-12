namespace Dignite.Abp.BlobManagement;

/// <summary>
/// The <see cref="Volo.Abp.BusinessException"/> codes the Blob Management throws; localized by <c>BlobManagementResource</c>.
/// </summary>
public static class BlobManagementErrorCodes
{
    public const string Namespace = "BlobManagement";

    /// <summary>
    /// The container is not one of the BlobManagement's: it was not declared with <c>UseBlobManagementContainer</c> on the Blob Management
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
    /// The ticket cannot be redeemed: not signed by this Blob Management, expired, already used, of the other kind, or its issuer
    /// may no longer use the container. Which of them is not told.
    /// </summary>
    public const string InvalidTicket = Namespace + ":InvalidTicket";

    /// <summary>
    /// The tenant's <c>BlobManagement.Enable</c> feature is off.
    /// </summary>
    public const string BlobManagementDisabled = Namespace + ":BlobManagementDisabled";

    /// <summary>
    /// Storing the content would take the tenant past its <c>BlobManagement.StorageSize</c>. Data: <c>StorageSize</c> (the
    /// allocation in megabytes), <c>UsedBytes</c>, <c>RequiredBytes</c>.
    /// </summary>
    public const string StorageQuotaExceeded = Namespace + ":StorageQuotaExceeded";
}
