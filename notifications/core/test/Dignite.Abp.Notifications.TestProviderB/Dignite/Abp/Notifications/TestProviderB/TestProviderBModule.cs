using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.TestProviderB;

[DependsOn(typeof(AbpNotificationsAbstractionsModule))]
public class TestProviderBModule : AbpModule
{
}

public class TestProviderBDefinitionProvider : NotificationDefinitionProvider
{
    public override void Define(INotificationDefinitionContext context)
    {
        context.AddGroup("Test.ProviderB").AddNotification(
            "Test.CrossModuleDuplicate",
            new FixedLocalizableString("Provider B"));
    }
}
