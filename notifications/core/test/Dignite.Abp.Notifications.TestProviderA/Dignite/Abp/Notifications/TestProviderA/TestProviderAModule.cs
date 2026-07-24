using System.Threading;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.TestProviderA;

[DependsOn(typeof(AbpNotificationsModule))]
public class TestProviderAModule : AbpModule
{
}

public class TestProviderADefinitionProvider : NotificationDefinitionProvider
{
    private static int _defineCallCount;

    public static int DefineCallCount => Volatile.Read(ref _defineCallCount);

    public static void ResetDefineCallCount()
    {
        Interlocked.Exchange(ref _defineCallCount, 0);
    }

    public override void Define(INotificationDefinitionContext context)
    {
        Interlocked.Increment(ref _defineCallCount);
        context.Add(new NotificationDefinition(
            "Test.CrossModuleDuplicate",
            new FixedLocalizableString("Provider A")));
    }
}
