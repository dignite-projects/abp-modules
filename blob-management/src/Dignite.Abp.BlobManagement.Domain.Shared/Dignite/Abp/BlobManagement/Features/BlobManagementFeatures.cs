namespace Dignite.Abp.BlobManagement.Features;

/// <summary>
/// Blob Management's ABP Features: what a tenant is allocated. They are checked on Blob Management on every save - the only place
/// that sees a tenant's bytes across applications.
/// </summary>
public static class BlobManagementFeatures
{
    public const string GroupName = "BlobManagement";

    /// <summary>Whether the tenant may store files in Blob Management at all. Default: <c>true</c>.</summary>
    public const string Enable = GroupName + ".Enable";

    /// <summary>
    /// The most the tenant may store, in megabytes, counting each stored BLOB once however many descriptors share it.
    /// <c>0</c>, the default, is no limit.
    /// </summary>
    public const string StorageSize = GroupName + ".StorageSize";
}
