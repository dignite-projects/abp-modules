using Dignite.Abp.Notifications;
using Volo.Abp.Localization;

namespace Dignite.NotificationCenter;

public class TestNotificationDefinitionProvider : NotificationDefinitionProvider
{
    public const string TestChannel = "Test";

    public const string OrdersGroup = "Test.Orders";

    public const string SystemGroup = "Test.System";

    public const string EmptyGroup = "Test.Empty";

    public const string OrderShipped = "order.shipped";

    public const string Announcement = "system.announcement";

    public override void Define(INotificationDefinitionContext context)
    {
        context.AddGroup(OrdersGroup, new FixedLocalizableString("Orders"))
            .AddNotification(OrderShipped, new FixedLocalizableString("Order Shipped"))
            .UseChannels(TestChannel);

        context.AddGroup(SystemGroup, new FixedLocalizableString("System"))
            .AddNotification(Announcement, new FixedLocalizableString("Announcement"));

        context.AddGroup(EmptyGroup, new FixedLocalizableString("Empty"));
    }
}
