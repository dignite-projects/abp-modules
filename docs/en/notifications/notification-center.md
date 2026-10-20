```json
//[doc-seo]
{
    "Description": "The Notification Center of the Dignite Notifications module: a persistent per-user inbox, read/unread state, subscriptions, push devices and a REST API on EF Core or MongoDB."
}
```

# Notification Center

The Notification Center is the optional part of the Notifications module that **keeps** notifications. Installed next to the core framework, it adds:

* A persistent **inbox** per user, with read/unread state and inbox groups.
* **Subscriptions**: users subscribe to a notification, or to a notification about one entity, and receive it without the publisher naming them.
* A registry of the user's **push devices**, for the [push channel](push.md).
* A **REST API** for all of it, and [MVC](mvc-ui.md) and [Angular](angular-ui.md) UIs.

It is a layered ABP [application module](https://abp.io/docs/latest/modules), with EF Core and MongoDB providers. [Getting Started](getting-started.md) walks through the installation.

## Inbox

Every notification a user receives gets a **`UserNotification`** row in the user's inbox, which references the shared **`Notification`** row that holds the payload. A row is `Unread` until the user reads it, and it stays until the user deletes it: the Notification Center expires nothing by itself; see [Retention and Personal Data](retention-and-personal-data.md).

The inbox is grouped by the [groups](defining-notifications.md#groups) of the notification definitions. A group is listed while the user can receive one of its notifications or still has unread notifications in it. Notifications whose definition no longer exists are listed under the synthetic **Other** group, so they can still be read and deleted.

## Subscriptions

A user who subscribes to a notification receives it whenever it is published **for subscribers**, that is with `userIds: null`; see [Recipients](publishing-notifications.md#recipients). A subscription has one of two scopes:

* **The whole notification**: no entity. The user receives every notification of that definition.
* **One entity**: an entity type name and an entity id, both together. The user receives the notifications of that definition about that entity only.

A notification published without an entity reaches the subscribers of the whole notification. A notification about an entity reaches the subscribers of the whole notification **and** those of that entity; a user subscribed to both receives it once.

Users manage their own subscriptions in the UI or through the REST API. A user can only subscribe to a notification whose [requirements](defining-notifications.md#requirements) they meet, and the requirements are checked again at delivery. To subscribe a user from your own code, for example the customer of an order to the notifications about that order, use `NotificationSubscriptionManager`:

````csharp
await _notificationSubscriptionManager.SubscribeAsync(
    order.CustomerId,
    "MyProject.OrderStatusChanged",
    new NotificationEntityIdentifier("MyProject.Order", order.Id.ToString()));
````

`SubscribeToAllAvailableNotificationsAsync(userId)` subscribes a user to every notification available to them, for example when the user is created; `UnsubscribeAsync` removes exactly the given scope.

## REST API

The `Dignite.NotificationCenter.HttpApi` package exposes the current user's Notification Center under `/api/notification-center`. Every endpoint requires an authenticated user and works on the caller's own data.

| Method and route | Description |
|---|---|
| `GET notifications` | The caller's notifications, paged, filtered by `state`, `groupName`, `startDate` and `endDate`. |
| `GET notifications/unread-count` | The unread count, for the bell's badge. |
| `GET notifications/groups` | The caller's inbox groups, with the unread count of each, for the inbox tabs. |
| `POST notifications/{id}/mark-as-read` | Marks one notification as read. |
| `POST notifications/mark-all-as-read` | Marks all notifications as read. |
| `DELETE notifications/{id}` | Deletes one notification. |
| `DELETE notifications/read` | Deletes all **read** notifications; unread ones are kept. |
| `GET subscriptions` | The caller's subscriptions: one row per available notification for the whole-notification scope, and one row per entity subscription. |
| `POST subscriptions` | Subscribes to the scope in the body: `notificationName`, and `entityTypeName` with `entityId` for one entity. |
| `DELETE subscriptions` | Unsubscribes exactly the scope in the query. |
| `POST push-devices/register` | Registers or refreshes the caller's device: `{ "provider", "token" }`. See [Push Notifier](push.md#registering-devices). |
| `POST push-devices/unregister` | Forgets the caller's device. |

A client must use the full scope (name, entity type and entity id) of a subscription, rather than infer it from the notification name. Every notification and subscription also carries its `groupName` and localized `groupDisplayName`.

The application services behind the API are `IUserNotificationAppService`, `INotificationSubscriptionAppService` and `IPushDeviceAppService`. The `Dignite.NotificationCenter.HttpApi.Client` package provides C# [client proxies](https://abp.io/docs/latest/framework/api-development/dynamic-csharp-clients) for them, on the `NotificationCenter` remote service; the Angular package has generated proxies.

## Database Providers

### Common

#### Table / Collection Prefix and Schema

All tables and collections use the `Notif` prefix by default. Set the static properties of the `NotificationCenterDbProperties` class to change it, before the database model is created:

````csharp
NotificationCenterDbProperties.DbTablePrefix = "App";
NotificationCenterDbProperties.DbSchema = "notifications";
````

#### Connection String

The module uses the `NotificationCenter` [connection string](https://abp.io/docs/latest/framework/fundamentals/connection-strings). It falls back to the `Default` connection string when it is not defined.

### Entity Framework Core

#### Database Tables

* **NotifNotifications**: The notification payloads, shared by every recipient.
* **NotifUserNotifications**: The inbox rows, one per recipient.
* **NotifNotificationSubscriptions**: The subscriptions.
* **NotifPushDevices**: The push devices.

The module ships no migrations. Call `builder.ConfigureNotificationCenter()` in the `OnModelCreating` method of the `DbContext` your application creates its migrations with, and add a migration, as [Getting Started](getting-started.md#creating-the-database-tables) shows. At runtime the module uses its own `NotificationCenterDbContext`.

### MongoDB

#### Database Collections

* **NotifNotifications**
* **NotifUserNotifications**
* **NotifNotificationSubscriptions**
* **NotifPushDevices**

The collections and their indexes are created when the module's `NotificationCenterMongoDbContext` first builds its model.

## Internals

### Domain Layer

#### Aggregates

* `Notification`: The payload of a published notification, its name, severity, entity and tenant.
* `UserNotification`: One recipient's inbox row for a `Notification`, with its read/unread state.
* `NotificationSubscription`: A user's subscription to a notification, with an optional entity scope.
* `PushDevice`: An app installation of a user that can receive pushes.

The aggregates have no custom repository interfaces. `INotificationStore`, the contract the core framework uses for everything it reads and writes, is the module's extension boundary: the EF Core and MongoDB packages implement it, and an application can replace it.

### Distributed Event Handlers

* `GdprUserDataDeletionRequestedHandler`: Erases a user's data when ABP's `GdprUserDataDeletionRequestedEto` is published; see [Personal Data Erasure](retention-and-personal-data.md#personal-data-erasure).

## See Also

* [Delivery Guarantees](delivery-guarantees.md)
* [Retention and Personal Data](retention-and-personal-data.md)
* [MVC UI](mvc-ui.md)
* [Angular UI](angular-ui.md)
