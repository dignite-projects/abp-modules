using Dignite.NotificationCenter.Web.Host.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace Dignite.NotificationCenter.Web.Host.Permissions;

public class HostPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(HostPermissions.GroupName);



        //Define your own permissions here. Example:
        //myGroup.AddPermission(HostPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<HostResource>(name);
    }
}
