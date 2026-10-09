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

- **`core/`** — `Notifications.Abstractions, Notifications, Notifications.Client,
  Notifications.Domain[.Shared], Notifications.EntityFrameworkCore, Notifications.MongoDB, Notifications.Identity,
  Notifications.Emailing[.Identity], Notifications.SignalR, Notifications.Push[.Expo]` — ABP's layout for a framework
  feature (design doc §4.1): one `.Abstractions` with every contract and its null default, the in-process
  implementation under the plain name, remote publishing in `.Client`, the definition store as
  `.Domain.Shared` / `.Domain` / `.EntityFrameworkCore` / `.MongoDB`.
  `core/` never references NotificationCenter; `Notifications` works standalone via `NullNotificationStore`.
  **Contracts in Abstractions, the default implementation in `Notifications`, remote publishing in `Client`**:
  anything another package implements (`INotificationStore`, `INotificationPermissionChecker`) or a business module
  uses lives in Abstractions, so neither the Notification Center's store, `Notifications.Identity` nor a business
  module depends on the implementation package. A business module's module class depends on
  `AbpNotificationsAbstractionsModule` — `AbpNotificationsModule` is the implementation and would bring the
  distributor, the job and both event handlers into every process hosting it.
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
| `Notifications.Abstractions` | Every contract: payload types, the two distributed-event contracts (`NotificationDeliveryRequestedEto`, `NotificationPublishRequestedEto`), the notifier contract, definitions + static/dynamic definition stores, routing, `INotificationPublisher` / `INotificationStore` / `INotificationDistributor` / `INotificationPermissionChecker`, info records; the null defaults `NullNotificationPublisher` (late `TryAdd`), `NullNotificationStore`, `AlwaysGrantedNotificationPermissionChecker`, `NullDynamicNotificationDefinitionStore` | ABP Features, Localization, MultiTenancy.Abstractions, Json |
| `Notifications` | The default, in-process implementation: local publisher, distributor, distribution job, delivery + publish-request handlers, `NotificationSubscriptionManager`, `NotificationDistributionOptions`, the hosted-channel / stateless startup checks | Abstractions, ABP BackgroundJobs.Abstractions, EventBus |
| `Notifications.Client` | `RemoteNotificationPublisher` (one `NotificationPublishRequestedEto` per notification), `TryRegister`: the local publisher wins when both are installed | Abstractions, ABP EventBus |
| `Notifications.Domain.Shared` | Record column sizes, `NotificationDefinitionsChangedEto` | ABP EventBus.Abstractions |
| `Notifications.Domain` | Definition catalog after ABP's permission management domain: record entities, `StaticNotificationDefinitionSaver` (publishes `NotificationDefinitionsChangedEto`), `DynamicNotificationDefinitionStore` (replaces `NullDynamicNotificationDefinitionStore`), initializer, options | Abstractions, Domain.Shared, ABP Ddd.Domain |
| `Notifications.EntityFrameworkCore` | `NotifDefinitionGroups` / `NotifDefinitions` on the `NotificationCenter` connection string, `ConfigureNotificationDefinitionStore()` | Domain, ABP EF Core |
| `Notifications.MongoDB` | The same collections and unique name indexes on the same connection string (`[IgnoreMultiTenancy]` context), `ConfigureNotificationDefinitionStore()` on `IMongoModelBuilder` | Domain, ABP MongoDB |
| `Notifications.Identity` | Permission-checker impl | Abstractions, ABP Authorization, `IUserRoleFinder` (`Identity.Domain.Shared`) |
| `Notifications.Emailing` / `.SignalR` | Notifier plugins | Abstractions + channel SDK |
| `Notifications.Emailing.Identity` | Email address resolver | Emailing, ABP Identity |
| `Notifications.Push` | Device push notifier; `IPushDeviceStore` / `IPushProvider` seams | Abstractions |
| `Notifications.Push.Expo` | Expo Push Service provider | Push + `Microsoft.Extensions.Http` |
| `NotificationCenter.Domain.Shared` | Constants, enums | — |
| `NotificationCenter.Domain` | Aggregates | Domain.Shared, Notifications.Abstractions |
| `NotificationCenter.Application.Contracts` | DTOs, service interfaces | Domain.Shared, Abstractions |
| `NotificationCenter.Application` | AppServices | Application.Contracts, Domain, Notifications (the implementation: `NotificationSubscriptionManager`) |
| `NotificationCenter.HttpApi` / `.HttpApi.Client` | Explicit controllers / client proxies | Application.Contracts |
| `NotificationCenter.EntityFrameworkCore` / `.MongoDB` | `INotificationStore` impls | Domain |
| `NotificationCenter.Push` | `IPushDeviceStore` over the `PushDevice` registry | Domain, Notifications.Push |
| `NotificationCenter.Push.Identity` | Drops devices whose ABP login session ended | NotificationCenter.Push, ABP Identity |
| `NotificationCenter.Installer` | ABP Studio/Suite install entry point, embeds the module's `.abpmdl` | `Volo.Abp.VirtualFileSystem` |

Notifiers depend on **only** `Abstractions` + their channel SDK — that's what lets a channel be added
without touching the pipeline.

Tests by project: `Dignite.Abp.Notifications.Tests` (core) · `Dignite.Abp.Notifications.Domain.TestBase`
(abstract provider-agnostic catalog scenarios: several named applications sharing one database and cache; the
provider module joins each application as a plug-in) · `Dignite.Abp.Notifications.EntityFrameworkCore.Tests` (SQLite) /
`.MongoDB.Tests` (embedded mongod) · `NotificationCenter.TestBase` (abstract provider-agnostic scenarios) ·
`.EntityFrameworkCore.Tests` / `.MongoDB.Tests` (per provider).

## Two operation modes

1. **Stateless forwarding** — `Notifications` (the implementation) + Notifiers, no persistence
   (`NullNotificationStore`), explicit `UserIds` only.
2. **Full Notification Center** — + `NotificationCenter` (+ EF Core or MongoDB): persistence,
   subscriptions, inbox, REST API.

The pipeline must work with `NullNotificationStore` alone.

Either mode can also serve **remote publishing**: a publisher process installs `Notifications.Client` instead of
`Notifications` and sends one `NotificationPublishRequestedEto` per notification (definition checked, channels
resolved and payload serialized in the publisher, through its outbox); the process running mode 1 or 2 handles it
with `NotificationPublishRequestedHandler` and distributes locally. The two packages do not exclude each other: in one
process the local publisher wins, whatever the module order. The receiving process knows the publishers' definitions
through `Notifications.Domain` + `.EntityFrameworkCore` or `.MongoDB` (publishers save at startup, the receiver reads with
`IsDynamicNotificationStoreEnabled`); a request for a name it cannot find is refused with an exception so the event
inbox retries it. Client and the implementation are separate packages, unlike ABP's `BackgroundJobs.RabbitMQ`, because a
publisher must not register the distribution job or the event handlers at all (design doc §4.2).

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
   The delivery handler (in `Notifications`) resolves only the notifier mapped to a delivery's channel; an
   unregistered notifier is never called.

## Commands

```bash
dotnet build Dignite.NotificationCenter.slnx
dotnet test Dignite.NotificationCenter.slnx

# core/ only (the MongoDB project starts an embedded mongod):
dotnet test core/test/Dignite.Abp.Notifications.Tests
dotnet test core/test/Dignite.Abp.Notifications.EntityFrameworkCore.Tests
dotnet test core/test/Dignite.Abp.Notifications.MongoDB.Tests

dotnet pack Dignite.NotificationCenter.slnx -c Release
```

No `DbMigrator` — a consuming host owns its own DbContext/migrations via
`ConfigureNotificationCenter(builder)` or `INotificationCenterDbContext` (and `ConfigureNotificationDefinitionStore(builder)`
for the definition store's tables).