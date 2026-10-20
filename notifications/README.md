# Dignite.Abp.Notifications

> Part of [**dignite-projects/abp-modules**](https://github.com/dignite-projects/abp-modules) — see
> the [repository README](../README.md) for the other modules, and
> [CONTRIBUTING.md](../CONTRIBUTING.md) for the build, versioning, and release process shared across
> them. Formerly developed at `dignite-projects/abp-notifications`; **no package ID changed** in the
> move.

An extensible, event-driven **notification framework for the [ABP Framework](https://abp.io)**, plus
an optional **Notification Center** (persistent inbox, subscriptions, read/unread state, REST API)
with **MVC** and **Angular** UI libraries.

- **Event-driven, pluggable notifiers.** The core publishes one `NotificationDeliveryRequestedEto` per recipient and
  channel; SignalR, email and device push (Expo) ship as separate packages, and a new channel needs no change to the
  core.
- **Two operation modes, one framework.** Run the in-process pipeline alone, with no database, or install the
  Notification Center for a persistent inbox, subscriptions, read/unread state and a REST API.
- **Split deployment.** A service that only publishes hands its notifications to a notification service through its
  outbox, and shares its definitions through the definition store.
- **Dual persistence.** EF Core and MongoDB implement the same inbox and the same definition store.
- **Contract-driven & headless.** Every payload carries a stable type discriminator, so any consumer — .NET, JS/TS,
  or the shipped UI libraries — can deserialize and render it.
- **Multi-tenant & permission-aware** — ABP `IMultiTenant` throughout, with recipient permission and feature checks.

> **.NET 10 · ABP 10.7 · LGPL-3.0-only**

## Documentation

**[Read the documentation](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/notifications/index.md)**
— getting started, defining and publishing notifications, routing, the SignalR, email and push channels, writing your
own channel, the Notification Center (subscriptions, REST API, delivery guarantees, retention and GDPR erasure), the
MVC and Angular UIs, and split deployment.

## Packages

**Core framework** (`core/`):

| Package | Purpose |
|---|---|
| `Dignite.Abp.Notifications.Abstractions` | Every contract and its null default: definitions, routing, `INotificationPublisher`, payload types, the distributed events, the notifier contract. **Business modules depend on only this.** |
| `Dignite.Abp.Notifications` | The default, in-process implementation: publisher, distributor, distribution job, event handlers. |
| `Dignite.Abp.Notifications.Client` | Remote publishing to a notification service. |
| `Dignite.Abp.Notifications.Domain.Shared` / `.Domain` / `.EntityFrameworkCore` / `.MongoDB` | The definition store shared by the services of a split deployment. |
| `Dignite.Abp.Notifications.SignalR` | Real-time notifier (hub at `/signalr-hubs/notifications`). |
| `Dignite.Abp.Notifications.Emailing` / `.Emailing.Identity` | Email notifier, and account email addresses from ABP Identity. |
| `Dignite.Abp.Notifications.Push` / `.Push.Expo` | Device push notifier, and the Expo Push Service provider. |
| `Dignite.Abp.Notifications.Identity` | Recipient permission checks through ABP authorization. |

**Optional Notification Center** (`notification-center/`):

| Package | Purpose |
|---|---|
| `Dignite.NotificationCenter.Domain.Shared` / `.Domain` | `Notification`, `UserNotification`, `NotificationSubscription`, `PushDevice`. |
| `Dignite.NotificationCenter.Application.Contracts` / `.Application` | Inbox, subscription and push device app services. |
| `Dignite.NotificationCenter.HttpApi` / `.HttpApi.Client` | REST API at `/api/notification-center`, and C# client proxies. |
| `Dignite.NotificationCenter.EntityFrameworkCore` / `.MongoDB` | Database providers. |
| `Dignite.NotificationCenter.Web` | MVC UI: notification bell, inbox page, subscription settings. |
| `Dignite.NotificationCenter.Push` / `.Push.Identity` | Push device registry, and push that follows the ABP login session. |
| `@dignite/ng.notification-center` (npm) | Angular UI: bell, inbox page, subscriptions, API proxies. |

The NuGet and npm packages always use the same version.

## Quick start

A monolith with the Notification Center on EF Core and the SignalR channel:

```bash
dotnet add package Dignite.Abp.Notifications.SignalR
dotnet add package Dignite.NotificationCenter.Application
dotnet add package Dignite.NotificationCenter.HttpApi
dotnet add package Dignite.NotificationCenter.EntityFrameworkCore
```

```csharp
[DependsOn(
    typeof(AbpNotificationsSignalRModule),
    typeof(AbpNotificationCenterApplicationModule),        // brings AbpNotificationsModule
    typeof(AbpNotificationCenterHttpApiModule),
    typeof(AbpNotificationCenterEntityFrameworkCoreModule))]
public class MyHostModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<NotificationRoutingOptions>(options =>
            options.ForNotification("MyProject.OrderShipped", SignalRNotifier.ChannelName));
    }
}
```

Add `builder.ConfigureNotificationCenter()` to your migration `DbContext`, define the notification in a
`NotificationDefinitionProvider`, and publish it:

```csharp
await _notificationPublisher.PublishAsync(
    "MyProject.OrderShipped",
    new MessageNotificationData($"Your order {order.Number} has shipped."),
    userIds: [order.CustomerId]);
```

The documentation's [Getting Started](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/notifications/getting-started.md)
walks through each step.

## Development

```bash
dotnet build Dignite.Abp.Notifications.slnx
dotnet test  Dignite.Abp.Notifications.slnx     # EF Core tests on in-memory SQLite, MongoDB tests on an embedded mongod
dotnet run --project host/Dignite.NotificationCenter.Web.Host   # runnable MVC demo host
```

`host/` and `angular/` are local-dev demos only and are never packed. The demo host's `appsettings.json` contains no
certificate or encryption passphrases: in Development it uses ABP's development signing certificate; outside
Development it requires `AuthServer:CertificatePassPhrase` and `StringEncryption:DefaultPassPhrase` (user-secrets,
environment variables or a secret store) and fails fast at startup without them.

## License

Licensed under [LGPL-3.0-only](https://github.com/dignite-projects/abp-modules/blob/main/LICENSE).
