namespace Dignite.Drive.Features;

/// <summary>
/// The Drive's ABP Features: what a tenant is allocated. They are checked on the Drive on every save - the only place
/// that sees a tenant's bytes across applications.
/// </summary>
public static class DriveFeatures
{
    public const string GroupName = "Drive";

    /// <summary>Whether the tenant may store files in the Drive at all. Default: <c>true</c>.</summary>
    public const string Enable = GroupName + ".Enable";

    /// <summary>
    /// The most the tenant may store, in megabytes, counting each stored BLOB once however many descriptors share it.
    /// <c>0</c>, the default, is no limit.
    /// </summary>
    public const string StorageSize = GroupName + ".StorageSize";
}
