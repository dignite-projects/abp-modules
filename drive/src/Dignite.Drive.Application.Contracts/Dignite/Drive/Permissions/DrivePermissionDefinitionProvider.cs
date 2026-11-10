using Dignite.Drive.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.Drive.Permissions;

public class DrivePermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(DrivePermissions.GroupName, L("Permission:Drive"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<DriveResource>(name);
    }
}
