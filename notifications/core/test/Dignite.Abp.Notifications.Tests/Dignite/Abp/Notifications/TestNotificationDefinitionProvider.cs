using Volo.Abp.Localization;

namespace Dignite.Abp.Notifications;

public class TestNotificationDefinitionProvider : NotificationDefinitionProvider
{
    public const string GroupName = "Test";

    public const string Plain = "Test.Plain";
    public const string FeatureGated = "Test.FeatureGated";
    public const string DisabledFeatureGated = "Test.DisabledFeatureGated";
    public const string PermissionGranted = "Test.PermissionGranted";
    public const string PermissionDenied = "Test.PermissionDenied";

    public override void Define(INotificationDefinitionContext context)
    {
        var group = context.AddGroup(GroupName, new FixedLocalizableString("Test"));

        group.AddNotification(Plain, new FixedLocalizableString("Plain"));

        group.AddNotification(FeatureGated, new FixedLocalizableString("Feature Gated"))
            .RequireFeature(TestFeatureDefinitionProvider.EnabledFeature);

        group.AddNotification(DisabledFeatureGated, new FixedLocalizableString("Disabled Feature Gated"))
            .RequireFeature(TestFeatureDefinitionProvider.DisabledFeature);

        group.AddNotification(PermissionGranted, new FixedLocalizableString("Permission Granted"))
            .RequirePermission(TestNotificationPermissionChecker.GrantedPermission);

        group.AddNotification(PermissionDenied, new FixedLocalizableString("Permission Denied"))
            .RequirePermission(TestNotificationPermissionChecker.DeniedPermission);
    }
}
