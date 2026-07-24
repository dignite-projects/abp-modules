using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.TestProviderB;

[DependsOn(typeof(AbpNotificationsModule))]
public class TestProviderBModule : AbpModule
{
}

public class TestProviderBDefinitionProvider : NotificationDefinitionProvider
{
    public override void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(
            "Test.CrossModuleDuplicate",
            new FixedLocalizableString("Provider B")));
    }
}
