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

- **`core/`** — `Abstractions, Notifications, Notifications.Identity,
  Notifications.Emailing[.Identity], Notifications.SignalR`. Core never references
  NotificationCenter; works standalone via `NullNotificationStore`.
- **`notification-center/`** — `Domain.Shared, Domain, Application.Contracts, Application, HttpApi,
  HttpApi.Client, EntityFrameworkCore, MongoDB, Web`. `Web` = MVC UI (bell + subscriptions).
  `HttpApi` = explicit controllers under `/api/notification-center` (`UserNotificationController`,
  `NotificationSubscriptionController`) — not conventional/auto.
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
| `Notifications.Abstractions` | Data contracts + distributed-event contract | — |
| `Notifications` (Core) | Definitions, publish/distribute, `INotificationStore` abstraction | Abstractions |
| `Notifications.Identity` | Permission-checker impl | Core, ABP Identity |
| `Notifications.Emailing` / `.SignalR` | Notifier plugins | Abstractions + channel SDK |
| `Notifications.Emailing.Identity` | Email address resolver | Emailing, ABP Identity |
| `NotificationCenter.Domain.Shared` | Constants, enums | — |
| `NotificationCenter.Domain` | Aggregates | Domain.Shared, Core |
| `NotificationCenter.Application.Contracts` | DTOs, service interfaces | Domain.Shared, Abstractions |
| `NotificationCenter.Application` | AppServices | Application.Contracts, Domain |
| `NotificationCenter.HttpApi` / `.HttpApi.Client` | Explicit controllers / client proxies | Application.Contracts |
| `NotificationCenter.EntityFrameworkCore` / `.MongoDB` | `INotificationStore` impls | Domain |
| `NotificationCenter.Installer` | ABP Studio/Suite install entry point, embeds the module's `.abpmdl` | `Volo.Abp.VirtualFileSystem` |

Notifiers depend on **only** `Abstractions` + their channel SDK — that's what lets a channel be added
without touching Core.

Tests by project: `Dignite.Abp.Notifications.Tests` (core) · `NotificationCenter.TestBase` (abstract
provider-agnostic scenarios) · `.EntityFrameworkCore.Tests` / `.MongoDB.Tests` (per provider).

## Two operation modes

1. **Stateless forwarding** — `Notifications` + Notifiers, no persistence (`NullNotificationStore`),
   explicit `UserIds` only.
2. **Full Notification Center** — + `NotificationCenter` (+ EF Core or MongoDB): persistence,
   subscriptions, inbox, REST API.

Core logic must work with `NullNotificationStore` alone.

## Adding a feature

**New notification type** (most common, no Domain layer change):
1. `NotificationData` subclass with a stable `[NotificationDataType("...")]` discriminator — never
   the CLR type name. See `notifications-invariants` §1.
2. Register in `NotificationDataOptions`; define via `INotificationDefinitionProvider` (name, display
   text, feature/permission gating, `UseChannels(...)`).
3. Publish via `INotificationPublisher`. No entity/EF/Mongo change.

**New Notifier**:
1. New project `Dignite.Abp.Notifications.<Channel>` under `core/src/`, depending on
   `Notifications.Abstractions` only if possible.
2. Implement `INotificationNotifier`: stable `Name` + cancellation-aware
   `DeliverAsync(NotificationDeliveryRequestedEto, CancellationToken)`.
3. Module class `[DependsOn(typeof(AbpNotificationsAbstractionsModule), ...)]`.

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