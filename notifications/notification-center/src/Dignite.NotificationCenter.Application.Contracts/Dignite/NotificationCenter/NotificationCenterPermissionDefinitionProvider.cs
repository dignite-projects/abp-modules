using Dignite.NotificationCenter.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace Dignite.NotificationCenter;

public class NotificationCenterPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(
            NotificationCenterPermissions.GroupName,
            L("Permission:NotificationCenter"));
        var deliveries = group.AddPermission(
            NotificationCenterPermissions.Deliveries.Default,
            L("Permission:Deliveries"));
        deliveries.AddChild(
            NotificationCenterPermissions.Deliveries.Retry,
            L("Permission:Deliveries.Retry"));
        deliveries.AddChild(
            NotificationCenterPermissions.Deliveries.ForceDeliver,
            L("Permission:Deliveries.ForceDeliver"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<NotificationCenterResource>(name);
    }
}
