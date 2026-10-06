using Dignite.Abp.Notifications;
using Dignite.Abp.Notifications.SignalR;
using Volo.Abp.Localization;

namespace Dignite.NotificationCenter.Web.Host.Notifications;

/// <summary>
/// Registers the demo notification types so they appear on the subscriptions page and can be published.
/// A real app defines these in whichever business module raises them (see notifications/CLAUDE.md "Adding a feature").
/// </summary>
public class DemoNotificationDefinitionProvider : NotificationDefinitionProvider
{
    public override void Define(INotificationDefinitionContext context)
    {
        var orders = context.AddGroup("Demo.Orders", new FixedLocalizableString("Orders"));
        orders.AddNotification("Demo.OrderShipped", new FixedLocalizableString("Order shipped"))
            .UseChannels(SignalRNotifier.ChannelName);

        var system = context.AddGroup("Demo.System", new FixedLocalizableString("System"));
        system.AddNotification("Demo.Announcement", new FixedLocalizableString("Announcement"))
            .UseChannels(SignalRNotifier.ChannelName);
    }
}
