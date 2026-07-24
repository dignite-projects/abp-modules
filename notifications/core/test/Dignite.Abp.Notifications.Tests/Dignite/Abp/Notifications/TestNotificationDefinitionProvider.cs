using Dignite.Abp.Notifications.SignalR;
using Volo.Abp.Localization;

namespace Dignite.Abp.Notifications;

public class TestNotificationDefinitionProvider : NotificationDefinitionProvider
{
    public const string Plain = "Test.Plain";
    public const string FeatureGated = "Test.FeatureGated";
    public const string DisabledFeatureGated = "Test.DisabledFeatureGated";
    public const string PermissionGranted = "Test.PermissionGranted";
    public const string PermissionDenied = "Test.PermissionDenied";

    public override void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(Plain, new FixedLocalizableString("Plain"))
            .UseChannels(SignalRNotifier.ChannelName));

        context.Add(new NotificationDefinition(FeatureGated, new FixedLocalizableString("Feature Gated"))
            .UseChannels(SignalRNotifier.ChannelName)
            .RequireFeature(TestFeatureDefinitionProvider.EnabledFeature));

        context.Add(new NotificationDefinition(DisabledFeatureGated, new FixedLocalizableString("Disabled Feature Gated"))
            .UseChannels(SignalRNotifier.ChannelName)
            .RequireFeature(TestFeatureDefinitionProvider.DisabledFeature));

        context.Add(new NotificationDefinition(PermissionGranted, new FixedLocalizableString("Permission Granted"))
            .UseChannels(SignalRNotifier.ChannelName)
            .RequirePermission(TestNotificationPermissionChecker.GrantedPermission));

        context.Add(new NotificationDefinition(PermissionDenied, new FixedLocalizableString("Permission Denied"))
            .UseChannels(SignalRNotifier.ChannelName)
            .RequirePermission(TestNotificationPermissionChecker.DeniedPermission));
    }
}
