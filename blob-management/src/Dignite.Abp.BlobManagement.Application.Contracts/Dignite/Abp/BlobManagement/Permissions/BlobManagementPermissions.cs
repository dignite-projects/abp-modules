namespace Dignite.Abp.BlobManagement.Permissions;

/// <summary>
/// Blob Management's permissions. They authorize users, for the two things a user does here: read what the tenant
/// stores - the management page - and keep the directories of a container. Files are an application's: the BLOB API
/// and the file API authorize clients by each container's allow-list, and use none of these.
/// <para>
/// Reading is module-wide (<see cref="Containers"/>), granted to roles or users in ABP's permission dialog. Keeping
/// directories is granted on a container (<see cref="Container"/>) and nowhere else: there is no permission that
/// creates, renames or deletes directories in every container.
/// </para>
/// </summary>
public static class BlobManagementPermissions
{
    public const string GroupName = "BlobManagement";

    public static class Containers
    {
        /// <summary>
        /// The management permission: enter the management page and read everything there - the usage, every
        /// container, its directories and its files. It changes nothing.
        /// </summary>
        public const string Default = GroupName + ".Containers";
    }

    /// <summary>Manage the grants on containers (the management permission of the resource permissions below).</summary>
    public const string ManagePermissions = GroupName + ".ManagePermissions";

    /// <summary>
    /// Resource permissions on one container (<see cref="BlobManagementResourceNames.Container"/>, keyed by its name):
    /// what a user may do with the directories of this one.
    /// </summary>
    public static class Container
    {
        public const string Default = GroupName + ".Container";

        /// <summary>See the container's directory tree. The other three count only with it, or with the management permission.</summary>
        public const string View = Default + ".View";

        public const string CreateDirectories = Default + ".CreateDirectories";

        /// <summary>Rename and move directories.</summary>
        public const string UpdateDirectories = Default + ".UpdateDirectories";

        public const string DeleteDirectories = Default + ".DeleteDirectories";
    }

    /// <summary>The module-wide permissions - those granted in ABP's permission dialog.</summary>
    public static string[] GetAll()
    {
        return new[]
        {
            Containers.Default,
            ManagePermissions
        };
    }
}
