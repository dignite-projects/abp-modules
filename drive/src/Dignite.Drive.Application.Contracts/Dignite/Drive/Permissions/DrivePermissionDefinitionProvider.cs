using Dignite.Drive.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.Drive.Permissions;

/// <summary>The <c>Drive</c> group and its module-wide file permissions.</summary>
public class DrivePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(DrivePermissions.GroupName, L("Permission:Drive"));

        var files = group.AddPermission(DrivePermissions.Files.Default, L("Permission:Files"));
        files.AddChild(DrivePermissions.Files.Upload, L("Permission:Files.Upload"));
        files.AddChild(DrivePermissions.Files.Delete, L("Permission:Files.Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<DriveResource>(name);
    }
}
