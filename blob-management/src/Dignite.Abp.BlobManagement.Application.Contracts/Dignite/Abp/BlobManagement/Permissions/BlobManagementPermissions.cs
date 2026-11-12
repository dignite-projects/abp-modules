using Volo.Abp.Reflection;

namespace Dignite.Abp.BlobManagement.Permissions;

/// <summary>
/// Blob Management's permissions. They authorize users: the back office, the explorer a business system opens, the
/// file API. The BLOB API authorizes clients by each container's allow-list instead, and uses none of these.
/// <para>
/// Three levels, each the one above limited to less: <see cref="Containers"/>, module-wide and granted to roles or users
/// in ABP's permission dialog; <see cref="Container"/>, granted on one container; <see cref="Directory"/>, granted on
/// one directory that has permissions of its own. Reading a file by its id needs a signed-in user and nothing finer,
/// and the uploader of a file may delete it.
/// </para>
/// </summary>
public static class BlobManagementPermissions
{
    public const string GroupName = "BlobManagement";

    /// <summary>The module-wide permissions: they act in every container.</summary>
    public static class Containers
    {
        /// <summary>
        /// The management permission: enter the back office and see every container, with its directories and files -
        /// restricted directories included.
        /// </summary>
        public const string Default = GroupName + ".Containers";

        public const string UploadFiles = Default + ".UploadFiles";

        /// <summary>Rename and move files.</summary>
        public const string UpdateFiles = Default + ".UpdateFiles";

        /// <summary>Delete any file, not only one's own uploads.</summary>
        public const string DeleteFiles = Default + ".DeleteFiles";

        public const string CreateDirectories = Default + ".CreateDirectories";

        /// <summary>Rename and move directories.</summary>
        public const string UpdateDirectories = Default + ".UpdateDirectories";

        public const string DeleteDirectories = Default + ".DeleteDirectories";
    }

    /// <summary>
    /// Manage the grants on containers and directories (the management permission of the resource permissions below),
    /// and say which directories have permissions of their own.
    /// </summary>
    public const string ManagePermissions = GroupName + ".ManagePermissions";

    /// <summary>
    /// Resource permissions on one container (<see cref="BlobManagementResourceNames.Container"/>, keyed by its name):
    /// what <see cref="Containers"/> gives in every container, in this one.
    /// </summary>
    public static class Container
    {
        public const string Default = GroupName + ".Container";

        /// <summary>See the container, its directories and the files in them.</summary>
        public const string View = Default + ".View";

        public const string UploadFiles = Default + ".UploadFiles";

        public const string UpdateFiles = Default + ".UpdateFiles";

        public const string DeleteFiles = Default + ".DeleteFiles";

        public const string CreateDirectories = Default + ".CreateDirectories";

        public const string UpdateDirectories = Default + ".UpdateDirectories";

        public const string DeleteDirectories = Default + ".DeleteDirectories";
    }

    /// <summary>
    /// Resource permissions on one directory (<see cref="BlobManagementResourceNames.Directory"/>, keyed by its id).
    /// They count while the directory is restricted: it, and everything below it, is then seen - and its subdirectories
    /// managed - by those granted here. What may be done with the files in it stays the container's permission.
    /// </summary>
    public static class Directory
    {
        public const string Default = GroupName + ".Directory";

        /// <summary>See the directory and everything below it.</summary>
        public const string View = Default + ".View";

        public const string CreateDirectories = Default + ".CreateDirectories";

        public const string UpdateDirectories = Default + ".UpdateDirectories";

        public const string DeleteDirectories = Default + ".DeleteDirectories";
    }

    /// <summary>The module-wide permissions - those granted in ABP's permission dialog.</summary>
    public static string[] GetAll()
    {
        return new[]
        {
            Containers.Default,
            Containers.UploadFiles,
            Containers.UpdateFiles,
            Containers.DeleteFiles,
            Containers.CreateDirectories,
            Containers.UpdateDirectories,
            Containers.DeleteDirectories,
            ManagePermissions
        };
    }
}
