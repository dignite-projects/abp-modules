```json
//[doc-seo]
{
    "Description": "Define notification payloads with stable type names and notification definitions with groups, permission and feature requirements in the Dignite Notifications module."
}
```

# Defining Notifications

A notification has two parts you define once: a **payload type** for the data it carries, and a **definition** that names it, groups it and says who may receive it. Both live in `Dignite.Abp.Notifications.Abstractions`, so a business module defines its notifications without depending on how they are distributed.

## Payloads

A payload is a class that derives from `NotificationData` and declares a **stable type name** with the `NotificationDataType` attribute:

````csharp
[NotificationDataType("MyProject.OrderShipped")]
public class OrderShippedNotificationData : NotificationData
{
    public string OrderNumber { get; set; } = default!;

    public int ItemCount { get; set; }
}
````

Register it in the `ConfigureServices` method of your module:

````csharp
Configure<NotificationDataOptions>(options =>
{
    options.Add<OrderShippedNotificationData>();
});
````

The payload is stored and sent as JSON with its type name in a `type` property, such as `{"type": "MyProject.OrderShipped", "orderNumber": "SO-1001", "itemCount": 3}`. That is what the inbox stores, what the REST API returns and what the SignalR channel pushes, so the type name is **part of your data**:

* **Never change a type name** once notifications with it are stored. The type name, not the CLR type name, is how a stored payload finds its class, so renaming or moving the class is safe and renaming the type name is not.
* Type names are compared ordinally and case-sensitively. Registering one type name for two classes, or one class under two type names, fails at application startup, naming both sides. Registering the same pair twice is harmless.
* Payloads are serialized with `System.Text.Json` only.

### Built-in Payloads

You do not need a payload type of your own for a simple message:

* `MessageNotificationData` (`Dignite.Message`): A plain `Message` text.
* `LocalizableMessageNotificationData` (`Dignite.LocalizableMessage`): A localization `ResourceName`, a `Name` (the key) and optional `Arguments`, localized when the notification is read, in the reader's language.

````csharp
await _notificationPublisher.PublishAsync(
    "MyProject.ReportReady",
    new LocalizableMessageNotificationData("MyProject", "Notification:ReportReady"),
    userIds: [userId]);
````

Both are registered by default, and the MVC and Angular UIs and the email and push channels render them out of the box. A payload of your own that implements `IHasNotificationImageUrl` (an `ImageUrl` property) gets its image shown on the default notification card of both UIs.

### Evolving a Payload

There is no version number in the payload. To change a payload's shape, **add members** rather than renaming or removing them: a stored payload of a known type is read leniently, and JSON properties that no longer map to a property are kept in its `ExtensionData` dictionary instead of being lost.

Every read is **tolerant**. A payload whose type name is not registered, or that cannot be read as its registered class, becomes an `UnsupportedNotificationData` that keeps the original type name (`OriginalDiscriminator`), the `Reason` (`UnknownDiscriminator` or `MalformedPayload`) and the original JSON (`RawJson`), instead of throwing. One bad stored row cannot break the rest of an inbox page, and the UIs show such a notification as a generic "unsupported notification" message. Writing a payload of an unregistered class, on the other hand, always throws.

`INotificationDataSerializer` is the service that reads and writes payloads, if you need to do it yourself.

## Notification Definitions

A notification is defined in a class that derives from `NotificationDefinitionProvider`, in the same way ABP [permissions](https://abp.io/docs/latest/framework/fundamentals/authorization#permission-system) are defined:

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
                LocalizableString.Create<MyProjectResource>("Notification:OrderShipped"))
            .WithDescription(LocalizableString.Create<MyProjectResource>("Notification:OrderShipped:Description"));

        orders.AddNotification(
                "MyProject.RefundRequested",
                LocalizableString.Create<MyProjectResource>("Notification:RefundRequested"))
            .RequirePermission("MyProject.Refunds.Manage");
    }
}
````

Providers are discovered by convention across all modules, and each runs once. To register one explicitly, add it to `NotificationDefinitionRegistration.DefinitionProviders`.

### Groups

Every notification belongs to a **group**, which is how the inbox tabs and the subscription settings arrange notifications. A group exists only in the definitions and is never stored, so moving a notification to another group needs no data migration.

To add notifications to a group another module created, use `context.GetGroupOrNull(name)` instead of `AddGroup`. Stored notifications whose definition no longer exists fall into a synthetic **Other** group, so they can still be listed and deleted.

### Names

Group and notification names are compared ordinally and case-sensitively. A duplicate group name, or a duplicate notification name in any group, is a startup error that names both providers. Empty and whitespace-only names are rejected.

The notification name is stored with every notification and subscription, so treat it like a payload type name: use a stable, prefixed name such as `MyProject.OrderShipped`, and do not change it.

### Requirements

A definition can require a [permission](https://abp.io/docs/latest/framework/fundamentals/authorization) or a [feature](https://abp.io/docs/latest/framework/infrastructure/features) from its recipients:

* `RequirePermission(permissionName)`: Only users granted the permission receive the notification or can subscribe to it.
* `RequireFeature(featureName)`: Only users of a tenant with the feature enabled receive it or can subscribe to it.

The requirements are checked for **every recipient, at delivery**, including the users you name explicitly when publishing; see [Recipient Eligibility](publishing-notifications.md#recipient-eligibility). Permission checks need the `Dignite.Abp.Notifications.Identity` package; without it, every user is granted.

### Other Properties

* `WithDescription(description)`: A longer, localizable description, shown in the subscription settings.
* `WithAttribute(key, value)`: An arbitrary attribute, for your own code to read from the definition.

A definition says nothing about **delivery channels**. Which channels carry a notification is configured separately; see [Routing to Channels](routing.md).

## Reading the Definitions

`INotificationDefinitionManager` returns the definitions, with the asynchronous `GetAsync`, `GetOrNullAsync`, `GetAllAsync`, `GetGroupsAsync` and `GetGroupOrNullAsync` methods. In a [split deployment](split-deployment.md), it also returns the definitions other services saved to the definition store.

## See Also

* [Publishing Notifications](publishing-notifications.md)
* [Routing to Channels](routing.md)
