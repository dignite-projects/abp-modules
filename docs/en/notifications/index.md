```json
//[doc-seo]
{
    "Description": "An event-driven notification framework for ABP Framework, with pluggable SignalR, email and push channels and an optional Notification Center with an inbox and subscriptions."
}
```

# Notifications Module

The Notifications module is an **event-driven notification framework** for ABP applications. A module defines its notifications and publishes them with `INotificationPublisher`; the framework resolves the recipients, checks that each one may receive the notification, and hands it to the **channels** your application has installed: real-time push to the browser over SignalR, email, and push to phones.

An optional **Notification Center** adds what an application needs once notifications have to be kept: a persistent per-user inbox, read/unread state, subscriptions, a REST API, and Angular and MVC UI with a notification bell.

* **Pluggable channels.** Each channel is a package of its own, and a notification is routed to channels by configuration, not by its definition. A new channel can be added without touching the notifications that already exist.
* **Two operation modes.** Run the framework alone, with no database, to forward notifications to connected users, or add the Notification Center for an inbox and subscriptions. The publishing code is the same in both.
* **Split deployment.** A service that only publishes can hand its notifications to another process that hosts the inbox and the channels, through the distributed event bus and its outbox.
* **EF Core and MongoDB.** Both are supported for the Notification Center and the definition store.
* **Contract-driven payloads.** Every payload carries a stable type name, so any client, .NET or not, can read and render it.
* **Multi-tenant and permission-aware.** Notifications are tenant-scoped, and a notification can require a permission or a feature from its recipients.

> The [Introducing Dignite ABP Notifications](../articles/2026-07-16-introducing-abp-notification-center/post.md) article tells the story of the module's design.

## How It Works

````text
INotificationPublisher.PublishAsync(...)
   │  the payload is serialized once
   ▼
Distributor ── resolves recipients: the given user ids, or the subscribers
   │           filters them: the notification's permission and feature requirements
   │           writes inbox rows (Notification Center only)
   ▼
NotificationDeliveryRequestedEto ── one per recipient and channel
   │
   ▼
Notifier of the channel ── SignalR, Email, Push, or your own
````

1. Business code calls `INotificationPublisher.PublishAsync` with a notification name, an optional payload and the recipients.
2. A notification for a few explicit users is distributed inline; anything larger, including notifications for subscribers, is distributed by a [background job](https://abp.io/docs/latest/framework/infrastructure/background-jobs).
3. The distributor works through the recipients in bounded batches. It drops those that do not meet the notification's requirements, writes their inbox rows when the Notification Center is installed, and publishes one `NotificationDeliveryRequestedEto` per recipient and channel on the [distributed event bus](https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed).
4. The process that hosts the channel handles the event and calls the channel's notifier once. Delivery is best-effort: a notifier that fails is logged and not retried, and the inbox row is the authoritative record.

## Operation Modes

### Stateless Forwarding

Install the `Dignite.Abp.Notifications` package and at least one channel. There is no inbox and no subscription: you publish to explicit user ids, the channel delivers to them, and nothing is stored. This fits an application that only needs to tell connected users that something happened.

### Notification Center

Also install the Notification Center and a database provider. Every notification is stored in the recipients' inboxes, users can subscribe to notifications and see what they missed, and the bell in the UI shows the unread count. See [Notification Center](notification-center.md).

## Packages

The **core framework** is laid out the way ABP lays out its own framework features, such as authorization and background jobs:

| Package | Description |
|---|---|
| `Dignite.Abp.Notifications.Abstractions` | Every contract: notification definitions, routing, `INotificationPublisher`, the payload types, the distributed events and the notifier contract, with a null default for each service. **A business module depends on this package only.** |
| `Dignite.Abp.Notifications` | The default, in-process implementation: the publisher, the distributor, its background job and the event handlers. Installed by the application that delivers the notifications. |
| `Dignite.Abp.Notifications.Client` | A publisher that sends notifications to another process to distribute. See [Split Deployment](split-deployment.md). |
| `Dignite.Abp.Notifications.Domain.Shared`, `.Domain`, `.EntityFrameworkCore`, `.MongoDB` | The definition store, which shares notification definitions between the services of a [split deployment](split-deployment.md). |
| `Dignite.Abp.Notifications.SignalR` | The [SignalR channel](signalr.md). |
| `Dignite.Abp.Notifications.Emailing` | The [email channel](email.md), on ABP's `IEmailSender`. |
| `Dignite.Abp.Notifications.Emailing.Identity` | Email addresses from ABP Identity user accounts, for the email channel. |
| `Dignite.Abp.Notifications.Push` | The [device push channel](push.md). |
| `Dignite.Abp.Notifications.Push.Expo` | The Expo Push Service provider for the push channel, for iOS and Android. |
| `Dignite.Abp.Notifications.Identity` | Permission checks for the recipients of a notification, through ABP's authorization system. See [Recipient Eligibility](publishing-notifications.md#recipient-eligibility). |

The **Notification Center** is a layered ABP module built on the core framework:

| Package | Description |
|---|---|
| `Dignite.NotificationCenter.Domain.Shared`, `.Domain` | The `Notification`, `UserNotification`, `NotificationSubscription` and `PushDevice` aggregates. |
| `Dignite.NotificationCenter.Application.Contracts`, `.Application` | The inbox, subscription and push device application services. |
| `Dignite.NotificationCenter.HttpApi`, `.HttpApi.Client` | The REST API at `/api/notification-center`, and C# client proxies for it. |
| `Dignite.NotificationCenter.EntityFrameworkCore`, `.MongoDB` | The database providers. |
| `Dignite.NotificationCenter.Web` | The [MVC UI](mvc-ui.md): the notification bell, the inbox page and the subscription settings. |
| `Dignite.NotificationCenter.Push`, `.Push.Identity` | The push device registry for the push channel, and ending push when the user's login session ends. |
| `@dignite/ng.notification-center` (npm) | The [Angular UI](angular-ui.md). |

> The core framework never references the Notification Center: the two are installed independently, and the core framework works without any database.

## Choosing a Topology

| Application | Installs | Publisher |
|---|---|---|
| **Monolith**: publishes, distributes and delivers in one process | `Dignite.Abp.Notifications` and the channels, plus the Notification Center for an inbox | Local |
| **Publisher service**: its notifications are distributed by a notification service | `Dignite.Abp.Notifications.Client` and the definition store | Remote |
| **Notification service**: distributes and delivers for the publishers | `Dignite.Abp.Notifications`, the Notification Center, the channels and the definition store | Local, plus the handler for remote publish requests |

Start with [Getting Started](getting-started.md) for a monolith. [Split Deployment](split-deployment.md) covers the other two.

## Upgrading from 3.x

The 10.x packages are a from-scratch rewrite of the earlier `Dignite.Abp.Notifications*` and `Dignite.NotificationCenter*` 3.x packages, not a compatible upgrade. Package and module names, payload serialization and the database schema all changed, and there is no automated migration of 3.x data. Treat a move from 3.x as replacing one module with another.

## See Also

* [Getting Started](getting-started.md)
* [Distributed Event Bus](https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed)
