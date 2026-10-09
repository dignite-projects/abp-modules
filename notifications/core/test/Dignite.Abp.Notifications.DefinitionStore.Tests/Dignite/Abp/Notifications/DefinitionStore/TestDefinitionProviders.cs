using System.Collections.Generic;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>
/// Publisher A's definitions. Display texts name a localization resource ("PublisherA") that only publisher A would
/// register; the notification service resolves them by that name.
/// </summary>
[DisableConventionalRegistration]
public class PublisherADefinitionProvider : NotificationDefinitionProvider
{
    public const string ResourceName = "PublisherA";

    public const string GroupName = "PublisherA.Orders";

    public const string OrderShipped = "PublisherA.OrderShipped";

    public const string OrderCancelled = "PublisherA.OrderCancelled";

    public const string OrdersPermission = "PublisherA.Orders.Read";

    public const string OrdersFeature = "PublisherA.Orders";

    public override void Define(INotificationDefinitionContext context)
    {
        var group = context.AddGroup(GroupName, LocalizableString.Create("Notification:Group:Orders", ResourceName));

        group.AddNotification(OrderShipped, LocalizableString.Create("Notification:OrderShipped", ResourceName))
            .WithDescription(LocalizableString.Create("Notification:OrderShipped:Description", ResourceName))
            .RequirePermission(OrdersPermission)
            .WithAttribute("Priority", 2)
            .WithAttribute("Category", "orders")
            .WithAttribute("Lines", new List<int> { 1, 2 });

        group.AddNotification(OrderCancelled, new FixedLocalizableString("Order cancelled"))
            .RequireFeature(OrdersFeature);
    }
}

/// <summary>Publisher B's definitions.</summary>
[DisableConventionalRegistration]
public class PublisherBDefinitionProvider : NotificationDefinitionProvider
{
    public const string GroupName = "PublisherB.Documents";

    public const string DocumentReady = "PublisherB.DocumentReady";

    public override void Define(INotificationDefinitionContext context)
    {
        context.AddGroup(GroupName, new FixedLocalizableString("Documents"))
            .AddNotification(DocumentReady, new FixedLocalizableString("Document ready"));
    }
}

/// <summary>A notification service's own definition of one of publisher A's names, without its requirement.</summary>
[DisableConventionalRegistration]
public class LocalCopyDefinitionProvider : NotificationDefinitionProvider
{
    public override void Define(INotificationDefinitionContext context)
    {
        context.AddGroup(PublisherADefinitionProvider.GroupName, new FixedLocalizableString("Orders (local)"))
            .AddNotification(PublisherADefinitionProvider.OrderShipped, new FixedLocalizableString("Shipped (local)"));
    }
}
