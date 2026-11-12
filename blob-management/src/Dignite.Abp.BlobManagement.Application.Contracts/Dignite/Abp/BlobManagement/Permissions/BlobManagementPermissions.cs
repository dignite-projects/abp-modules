using Volo.Abp.Reflection;

namespace Dignite.Abp.BlobManagement.Permissions;

/// <summary>
/// The permissions of the BlobManagement's file API (the interaction entry), module-wide and granted to users or roles. Reading
/// a file by its id needs a signed-in user and nothing finer, and the uploader of a file may delete it. The BLOB API
/// authorizes clients by each system container's allow-list instead, and uses none of these.
/// </summary>
public static class BlobManagementPermissions
{
    public const string GroupName = "BlobManagement";

    public static class Files
    {
        /// <summary>The parent of the file permissions; it grants nothing by itself.</summary>
        public const string Default = GroupName + ".Files";

        /// <summary>Upload files to any user drive.</summary>
        public const string Upload = Default + ".Upload";

        /// <summary>Delete any file of the user drives, not only one's own uploads.</summary>
        public const string Delete = Default + ".Delete";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(BlobManagementPermissions));
    }
}
