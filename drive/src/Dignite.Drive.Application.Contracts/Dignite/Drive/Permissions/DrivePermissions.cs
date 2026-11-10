using Volo.Abp.Reflection;

namespace Dignite.Drive.Permissions;

public class DrivePermissions
{
    public const string GroupName = "Drive";

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(DrivePermissions));
    }
}
