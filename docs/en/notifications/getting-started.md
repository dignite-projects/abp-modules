```json
//[doc-seo]
{
    "Description": "Install the Dignite Notifications module and the Notification Center in an ABP application, define a notification and publish it to a user's inbox and browser."
}
```

# Getting Started

This guide installs the Notifications module with the **Notification Center** in an ABP application that runs in one process, and sends a first notification to the inbox and, over SignalR, to the user's browser. It assumes an application created from one of ABP's [startup templates](https://abp.io/docs/latest/solution-templates), with Entity Framework Core.

## Installing the Packages

Install the packages into the layers of your solution. In a [single-layer](https://abp.io/docs/latest/solution-templates/single-layer-web-application) application, install them all into the one project.

| Layer | Packages |
|---|---|
| `Domain` | `Dignite.NotificationCenter.Domain` |
| `Application.Contracts` | `Dignite.NotificationCenter.Application.Contracts` |
| `Application` | `Dignite.NotificationCenter.Application` |
| `HttpApi` | `Dignite.NotificationCenter.HttpApi` |
| `EntityFrameworkCore` | `Dignite.NotificationCenter.EntityFrameworkCore` |
| `Web` or `HttpApi.Host` | `Dignite.Abp.Notifications.SignalR`, and `Dignite.NotificationCenter.Web` for an MVC UI |

For example:

````bash
dotnet add src/MyProject.Application/MyProject.Application.csproj package Dignite.NotificationCenter.Application
````

`Dignite.NotificationCenter.Application` brings `Dignite.Abp.Notifications`, the in-process implementation of the framework, with it. Then add the module dependencies to the module class of each layer, for example:

````csharp
[DependsOn(
    // ...other dependencies
    typeof(AbpNotificationCenterApplicationModule)
)]
public class MyProjectApplicationModule : AbpModule
{
}
````

The module classes are `AbpNotificationCenterDomainModule`, `AbpNotificationCenterApplicationContractsModule`, `AbpNotificationCenterApplicationModule`, `AbpNotificationCenterHttpApiModule`, `AbpNotificationCenterEntityFrameworkCoreModule`, `AbpNotificationsSignalRModule` and `AbpNotificationCenterWebModule`.

> For MongoDB, install `Dignite.NotificationCenter.MongoDB` and depend on `AbpNotificationCenterMongoDbModule` instead of the EF Core package. MongoDB creates its collections itself, so skip the next section.

## Creating the Database Tables

The Notification Center uses its own `DbContext` at runtime, on the `NotificationCenter` [connection string](https://abp.io/docs/latest/framework/fundamentals/connection-strings), which falls back to `Default`. Add its tables to the `DbContext` your application creates its migrations with, in the `OnModelCreating` method:

````csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    // ...the configuration of the other modules
    builder.ConfigureNotificationCenter();
}
````

Then add a migration and update the database, as for any other ABP module:

````bash
dotnet ef migrations add Added_NotificationCenter
````

The migration creates the `NotifNotifications`, `NotifUserNotifications`, `NotifNotificationSubscriptions` and `NotifPushDevices` tables. See [Database Tables](notification-center.md#database-tables) to change the prefix or the schema.

## Adding the UI

* **MVC / Razor Pages**: add the bell's script and style to your theme's global bundle; see [MVC UI](mvc-ui.md#installation).
* **Angular**: install `@dignite/ng.notification-center` and call `provideNotificationCenterConfig()`; see [Angular UI](angular-ui.md#installation).

Either UI adds the notification bell to the toolbar, an inbox page, and a subscriptions tab to the settings page.

## Defining a Notification

A module defines its notifications in a class that derives from `NotificationDefinitionProvider`. It is discovered automatically:

````csharp
public class MyProjectNotificationDefinitionProvider : NotificationDefinitionProvider
{
    public override void Define(INotificationDefinitionContext context)
    {
        var orders = context.AddGroup(
            "MyProject.Orders",
            LocalizableString.Create<MyProjectResource>("Notification:Orders"));

        orders.AddNotification(
            "MyProject.OrderShipped",
            LocalizableString.Create<MyProjectResource>("Notification:OrderShipped"));
    }
}
````

Every notification belongs to a **group**, which is how the inbox and the subscription settings arrange them. See [Defining Notifications](defining-notifications.md) for the payloads and requirements a notification can have.

## Routing It to a Channel

A definition does not say how a notification is delivered. Route it to the SignalR channel in the `ConfigureServices` method of a module:

````csharp
Configure<NotificationRoutingOptions>(options =>
{
    options.ForNotification("MyProject.OrderShipped", "SignalR");
});
````

A channel name is a plain string; `SignalRNotifier.ChannelName` is the same value, for a module that references the SignalR package anyway.

A notification without a route is still stored in the inbox, but no channel delivers it. See [Routing to Channels](routing.md).

## Publishing It

Inject `INotificationPublisher` and publish the notification when the order ships:

````csharp
public class OrderShippingService : DomainService
{
    private readonly INotificationPublisher _notificationPublisher;

    public OrderShippingService(INotificationPublisher notificationPublisher)
    {
        _notificationPublisher = notificationPublisher;
    }

    public async Task ShipAsync(Order order)
    {
        // ...ship the order

        await _notificationPublisher.PublishAsync(
            "MyProject.OrderShipped",
            new MessageNotificationData($"Your order {order.Number} has shipped."),
            entityIdentifier: new NotificationEntityIdentifier("MyProject.Order", order.Id.ToString()),
            severity: NotificationSeverity.Success,
            userIds: [order.CustomerId]);
    }
}
````

Run the application, sign in as the customer and ship an order: the bell shows the notification immediately, and it stays in the inbox until the user deletes it. See [Publishing Notifications](publishing-notifications.md) for the recipients, entities and severities.

> `MessageNotificationData` is the built-in payload for a plain message. For a notification of your own shape, define a payload type; see [Payloads](defining-notifications.md#payloads).

## Without the Notification Center

To forward notifications to connected users with no database at all, install `Dignite.Abp.Notifications` and a channel only:

````csharp
[DependsOn(
    typeof(AbpNotificationsModule),
    typeof(AbpNotificationsSignalRModule)
)]
public class MyProjectModule : AbpModule
{
}
````

The definitions and the publishing code are the same. In this mode there is no inbox to fall back to, so every notification needs a route, and the application checks that at startup. Subscriptions do not exist either, so a notification must name its recipients.

## Business Modules

A reusable module that defines and publishes notifications references `Dignite.Abp.Notifications.Abstractions` only, and depends on `AbpNotificationsAbstractionsModule`. **Not** on `Dignite.Abp.Notifications`: that is the implementation, and it would bring the distributor, its background job and the event handlers into every application that uses the module. The application decides how notifications are distributed.

## See Also

* [Defining Notifications](defining-notifications.md)
* [Notification Center](notification-center.md)
* [Split Deployment](split-deployment.md)
