namespace Dignite.Abp.BlobManagement.Permissions;

/// <summary>
/// Blob Management's permissions. Users hold them for the two things a user does here: read what the tenant stores -
/// the management page - and keep the directories of a container. Applications hold one: an OAuth client keeps the
/// files of a container through the BLOB API and the file API with <see cref="Container.Files"/> granted on it.
/// <para>
/// Reading is module-wide (<see cref="Containers"/>), granted to roles or users in ABP's permission dialog. Everything
/// else is granted on a container (<see cref="Container"/>) and nowhere else: there is no permission that keeps the
/// directories or the files of every container.
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
    /// what a user may do with the directories of this one, and whether an application may keep its files.
    /// </summary>
    public static class Container
    {
        public const string Default = GroupName + ".Container";

        /// <summary>
        /// For an OAuth client, and counted for nothing else (ABP's client provider, <c>C</c>): read, store, rename,
        /// move and delete the container's files through the BLOB API and the file API. Granted to a client, it is
        /// the deployment's, not a tenant's - a client is - so it holds in every tenant.
        /// </summary>
        public const string Files = Default + ".Files";

        /// <summary>See the container's directory tree. The three directory permissions count only with it, or with the management permission.</summary>
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
