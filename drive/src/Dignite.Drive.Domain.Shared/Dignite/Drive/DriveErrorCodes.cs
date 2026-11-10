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
    /// The tenant's <c>Drive.Enable</c> feature is off.
    /// </summary>
    public const string DriveDisabled = Namespace + ":DriveDisabled";

    /// <summary>
    /// Storing the content would take the tenant past its <c>Drive.StorageSize</c>. Data: <c>StorageSize</c> (the
    /// allocation in megabytes), <c>UsedBytes</c>, <c>RequiredBytes</c>.
    /// </summary>
    public const string StorageQuotaExceeded = Namespace + ":StorageQuotaExceeded";
}
