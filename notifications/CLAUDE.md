# Dignite.NotificationCenter

Event-driven notification framework for ABP Framework (LGPL-3.0-only), plus an optional Notification
Center (inbox/subscriptions/read-unread/REST API), an MVC UI library, and an Angular UI library. The
module's ABP Studio identity is `Dignite.NotificationCenter` (the app is the install entry point —
`core/` is an internal dependency of it, not a separately installed thing). Published: `core/`
(`Notifications*`, incl. `Abstractions`), `notification-center/src/` (incl.
`Dignite.NotificationCenter.Web` and `.Installer`), `angular/projects/notification-center`. `host/`
and the Angular demo app are local-dev-only, never packed.

## Structure

One `.slnx` — `Dignite.NotificationCenter.slnx`:

- **`core/`** — `Abstractions, Notifications, Notifications.Distribution, Notifications.Remote,
  Notifications.Identity, Notifications.Emailing[.Identity], Notifications.SignalR, Notifications.Push[.Expo]`.
  Core never references NotificationCenter; Core + Distribution works standalone via `NullNotificationStore`.
  **Contracts stay in Core, implementations go to Distribution**: anything another package implements
  (`INotificationStore`, `INotificationPermissionChecker`) or a business module uses stays in Core, so neither
  the Notification Center's store, `Notifications.Identity` nor a business module depends on Distribution.
- **`notification-center/`** — `Domain.Shared, Domain, Application.Contracts, Application, HttpApi,
  HttpApi.Client, EntityFrameworkCore, MongoDB, Web, Push[.Identity]`. `Web` = MVC UI (bell +
  subscriptions). `HttpApi` = explicit controllers under `/api/notification-center`
  (`UserNotificationController`, `NotificationSubscriptionController`, `PushDeviceController`) — not
  conventional/auto.
- **`host/`** — demo host (`Dignite.NotificationCenter.Web.Host`), no solution file:
  `dotnet run --project host/Dignite.NotificationCenter.Web.Host`. Own
  `Directory.Build.props`/`Directory.Packages.props` opting out of central package management.
  Relies on ABP's automatic hub mapping for `/signalr-hubs/notifications` (`AbpHub` — do not call
  `MapHub`). Uses EF migrations, not `EnsureCreated`.
- **`angular/`** — publishable `notification-center` lib (ABP-generated proxy + bell/subscriptions
  components) + demo app, npm-only, not in the `.slnx`.

Namespace-mirrored files: `<Project>/<namespace path>/File.cs`, `<RootNamespace/>` empty (test
projects that flatten to the project root are the exception).

| Project | Responsibility | Depends on |
|---|---|---|
| `Notifications.Abstractions` | Data contracts + the two distributed-event contracts (`NotificationDeliveryRequestedEto`, `NotificationPublishRequestedEto`) | — |
| `Notifications` (Core) | Definitions, routing, `INotificationPublisher` / `INotificationStore` / `INotificationDistributor` / `INotificationPermissionChecker` contracts, info records | Abstractions |
| `Notifications.Distribution` | Local publisher, distributor, distribution job, delivery + publish-request handlers, `NullNotificationStore`, `NotificationSubscriptionManager` | Core |
| `Notifications.Remote` | Remote `INotificationPublisher` (one `NotificationPublishRequestedEto` per notification); refuses to start next to Distribution | Core |
| `Notifications.Identity` | Permission-checker impl | Core, ABP Identity |
| `Notifications.Emailing` / `.SignalR` | Notifier plugins | Abstractions + channel SDK |
| `Notifications.Emailing.Identity` | Email address resolver | Emailing, ABP Identity |
| `Notifications.Push` | Device push notifier; `IPushDeviceStore` / `IPushProvider` seams | Abstractions |
| `Notifications.Push.Expo` | Expo Push Service provider | Push + `Microsoft.Extensions.Http` |
| `NotificationCenter.Domain.Shared` | Constants, enums | — |
| `NotificationCenter.Domain` | Aggregates | Domain.Shared, Core |
| `NotificationCenter.Application.Contracts` | DTOs, service interfaces | Domain.Shared, Abstractions |
| `NotificationCenter.Application` | AppServices | Application.Contracts, Domain, Distribution |
| `NotificationCenter.HttpApi` / `.HttpApi.Client` | Explicit controllers / client proxies | Application.Contracts |
| `NotificationCenter.EntityFrameworkCore` / `.MongoDB` | `INotificationStore` impls | Domain |
| `NotificationCenter.Push` | `IPushDeviceStore` over the `PushDevice` registry | Domain, Notifications.Push |
| `NotificationCenter.Push.Identity` | Drops devices whose ABP login session ended | NotificationCenter.Push, ABP Identity |
| `NotificationCenter.Installer` | ABP Studio/Suite install entry point, embeds the module's `.abpmdl` | `Volo.Abp.VirtualFileSystem` |

Notifiers depend on **only** `Abstractions` + their channel SDK — that's what lets a channel be added
without touching Core.

Tests by project: `Dignite.Abp.Notifications.Tests` (core) · `NotificationCenter.TestBase` (abstract
provider-agnostic scenarios) · `.EntityFrameworkCore.Tests` / `.MongoDB.Tests` (per provider).

## Two operation modes

1. **Stateless forwarding** — `Notifications.Distribution` + Notifiers, no persistence (`NullNotificationStore`),
   explicit `UserIds` only.
2. **Full Notification Center** — + `NotificationCenter` (+ EF Core or MongoDB): persistence,
   subscriptions, inbox, REST API.

Core logic must work with `NullNotificationStore` alone.

Either mode can also serve **remote publishing**: a publisher process installs `Notifications.Remote` instead of
Distribution and sends one `NotificationPublishRequestedEto` per notification (definition checked, channels resolved
and payload serialized in the publisher, through its outbox); the process running mode 1 or 2 handles it with
`NotificationPublishRequestedHandler` and distributes locally. Remote and Distribution never share a process.

## Adding a feature

**New notification type** (most common, no Domain layer change):
1. `NotificationData` subclass with a stable `[NotificationDataType("...")]` discriminator — never
   the CLR type name. See `notifications-invariants` §1.
2. Register in `NotificationDataOptions`; define via `INotificationDefinitionProvider` —
   `context.AddGroup(...).AddNotification(...)` (every definition belongs to a group, as with ABP
   permissions; name, display text, feature/permission gating). Groups are
   definition-time metadata only, never persisted. Delivery channels are not part of the definition: route
   with `Configure<NotificationRoutingOptions>(o => o.ForNotification(name, channels...))` (module default,
   host overrides; `InboxOnly(...)` for none).
3. Publish via `INotificationPublisher`. No entity/EF/Mongo change.

**New Notifier**:
1. New project `Dignite.Abp.Notifications.<Channel>` under `core/src/`, depending on
   `Notifications.Abstractions` only if possible.
2. Implement `INotificationNotifier`: stable `Name` + cancellation-aware
   `DeliverAsync(NotificationDeliveryRequestedEto, CancellationToken)`.
3. Module class `[DependsOn(typeof(AbpNotificationsAbstractionsModule), ...)]` that registers the
   channel: `Configure<NotificationNotifierOptions>(o => o.Notifiers.Add<TNotifier>(TNotifier.ChannelName))`.
   Distribution's delivery handler resolves only the notifier mapped to a delivery's channel; an unregistered
   notifier is never called.

## Commands

```bash
dotnet build Dignite.NotificationCenter.slnx
dotnet test Dignite.NotificationCenter.slnx

# Core only, skips embedded-mongod tests:
dotnet test core/test/Dignite.Abp.Notifications.Tests

dotnet pack Dignite.NotificationCenter.slnx -c Release
```

No `DbMigrator` — a consuming host owns its own DbContext/migrations via
`ConfigureNotificationCenter(builder)` or `INotificationCenterDbContext`.