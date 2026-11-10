namespace Dignite.Drive;

/// <summary>
/// The <see cref="Volo.Abp.BusinessException"/> codes the Drive throws; localized by <c>DriveResource</c>.
/// </summary>
public static class DriveErrorCodes
{
    public const string Namespace = "Drive";

    /// <summary>
    /// The container is not one of the Drive's: it was not declared with <c>UseDriveContainer</c> on the Drive
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
    /// The ticket cannot be redeemed: not signed by this Drive, expired, already used, of the other kind, or its issuer
    /// may no longer use the container. Which of them is not told.
    /// </summary>
    public const string InvalidTicket = Namespace + ":InvalidTicket";

    /// <summary>
    /// The tenant's <c>Drive.Enable</c> feature is off.
    /// </summary>
    public const string DriveDisabled = Namespace + ":DriveDisabled";

    /// <summary>
    /// Storing the content would take the tenant past its <c>Drive.StorageSize</c>. Data: <c>StorageSize</c> (the
    /// allocation in megabytes), <c>UsedBytes</c>, <c>RequiredBytes</c>.
    /// </summary>
    public const string StorageQuotaExceeded = Namespace + ":StorageQuotaExceeded";
}
