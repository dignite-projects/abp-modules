using Dignite.Abp.Notifications;
using Volo.Abp.Localization;

namespace Dignite.NotificationCenter;

public class TestNotificationDefinitionProvider : NotificationDefinitionProvider
{
    public const string TestChannel = "Test";

    public override void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition("order.shipped", new FixedLocalizableString("Order Shipped"))
            .UseChannels(TestChannel));
    }
}
