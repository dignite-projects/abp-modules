using Volo.Abp.Features;
using Volo.Abp.Localization;

namespace Dignite.Abp.Notifications;

public class TestFeatureDefinitionProvider : FeatureDefinitionProvider
{
    public const string EnabledFeature = "Test.EnabledFeature";
    public const string DisabledFeature = "Test.DisabledFeature";

    public override void Define(IFeatureDefinitionContext context)
    {
        var group = context.AddGroup("DigniteNotificationsTest", new FixedLocalizableString("Dignite Notifications Test"));

        group.AddFeature(EnabledFeature, defaultValue: "true", displayName: new FixedLocalizableString("Enabled"));
        group.AddFeature(DisabledFeature, defaultValue: "false", displayName: new FixedLocalizableString("Disabled"));
    }
}
