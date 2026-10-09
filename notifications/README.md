# Dignite.Abp.Notifications

> Part of [**dignite-projects/abp-modules**](https://github.com/dignite-projects/abp-modules) — see
> the [repository README](../README.md) for the other modules, and
> [CONTRIBUTING.md](../CONTRIBUTING.md) for the build, versioning, and release process shared across
> them. Formerly developed at `dignite-projects/abp-notifications`; **no package ID changed** in the
> move.

An extensible, event-driven **notification framework for the [ABP Framework](https://abp.io)**, plus
an optional **Notification Center** (persistent inbox, subscriptions, read/unread state, REST API)
with **MVC** and **Angular** UI libraries.

- **Event-driven, pluggable notifiers.** The core publishes one stable, independently claimable
  `NotificationDeliveryRequestedEto` per recipient and channel. Channels can be added, removed, or deployed
  independently without touching the core.
- **Two operation modes, one framework.** Run Core-only with process-local delivery state and no inbox,
  or install Notification Center for durable delivery state, persistent inbox, subscriptions,
  read/unread state, REST API, and operator retry.
- **Split deployment.** A service that only publishes can hand its notifications to another process that hosts
  the inbox and the channels: one `NotificationPublishRequestedEto` per notification, through the publisher's
  outbox (see [Split deployment](#split-deployment)).
- **Dual persistence.** EF Core and MongoDB implement the same inbox and delivery-state abstractions.
- **Contract-driven & headless.** Every payload carries a stable type discriminator, so any
  consumer — .NET, JS/TS, or the shipped Angular library — can deserialize and render it. The
  Notification Center is headless (REST API); UI is optional.
- **Multi-tenant & permission-aware** — ABP `IMultiTenant` throughout, with optional ABP Identity
  permission gating.

> **.NET 10 · ABP 10.5.0 · LGPL-3.0-only**

## Packages

Requirements: the **.NET 10 SDK** and an ABP **10.5.0** host application. Contract layers
multi-target `netstandard2.0;netstandard2.1;net10.0` so remote and older consumers can reference
them.

### Compatibility

| Dignite release | ABP Framework | Runtime | Angular library | Angular peer range |
|---|---|---|---|---|
| `10.0.0-rc.4` | `10.5.x` (built against `10.5.0`) | .NET 10 | `10.0.0-rc.4` | Angular `^21.2.0` |

The NuGet and npm packages always use the same version. Pre-release npm packages use the `next`
dist-tag; stable releases use `latest`. npm requires every package to have a `latest` tag, so until
the first stable version exists the initial pre-release is necessarily also exposed as `latest`.

**Core framework** (`core/`):

| Package | Purpose |
|---|---|
| `Dignite.Abp.Notifications.Abstractions` | Shared contracts: `NotificationData`, `NotificationDeliveryRequestedEto`, `NotificationPublishRequestedEto`, `[NotificationDataType]`, `INotificationDefinitionProvider`, `INotificationNotifier`. Notifiers and remote clients depend on **only** this. |
| `Dignite.Abp.Notifications` | The core business modules reference: definitions, routing (`NotificationRoutingOptions`, `INotificationChannelResolver`), the `INotificationPublisher` contract, and the `INotificationStore` / `INotificationDistributor` / `INotificationPermissionChecker` contracts. It implements none of the pipeline. |
| `Dignite.Abp.Notifications.Distribution` | The in-process pipeline: the local `INotificationPublisher`, the distributor, the distribution background job, the delivery and publish-request event handlers, `NullNotificationStore`. Installed by the process that hosts the inbox and the channels. |
| `Dignite.Abp.Notifications.Remote` | Remote publishing: an `INotificationPublisher` that sends one `NotificationPublishRequestedEto` per notification to the process that distributes. For publishers that do not host the inbox; never installed together with Distribution. |
| `Dignite.Abp.Notifications.DefinitionStore` | The definition store, after ABP's dynamic permission store: every process saves the definitions its modules register to shared tables at startup, and a process with `IsDynamicNotificationStoreEnabled` reads everyone's. Installed by the publishers (to save) and the notification service (to save and read) of a [split deployment](#split-deployment). |
| `Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore` | The store's EF Core tables (`NotifDefinitionGroups`, `NotifDefinitions`) on the `NotificationCenter` connection string, with `ConfigureNotificationDefinitionStore()` for the host's migration DbContext. A MongoDB implementation is a follow-up. |
| `Dignite.Abp.Notifications.SignalR` | Real-time push notifier (SignalR hub at `/signalr-hubs/notifications`). |
| `Dignite.Abp.Notifications.Emailing` | Email notifier (ABP `IEmailSender`). |
| `Dignite.Abp.Notifications.Emailing.Identity` | Optional ABP Identity-backed email address resolver for the Emailing notifier. |
| `Dignite.Abp.Notifications.Push` | Device push notifier (the `"Push"` channel): content chain, `IPushDeviceStore` and `IPushProvider` contracts. |
| `Dignite.Abp.Notifications.Push.Expo` | Expo Push Service provider for the Push notifier (iOS + Android through one API). |
| `Dignite.Abp.Notifications.Identity` | Permission gating (`RequirePermission(...)`) through ABP authorization. Reads the recipient's roles from ABP's `IUserRoleFinder` (`Volo.Abp.Identity.Domain.Shared`), so it needs no Identity database; the host supplies the implementation, see [Recipient eligibility](#recipient-eligibility). |

**Optional Notification Center** (`notification-center/`) — persistence + REST API + UI, depends on
Core:

| Package | Purpose |
|---|---|
| `Dignite.NotificationCenter.Domain.Shared` | Enums / constants (`NotificationSeverity`, `UserNotificationState`). |
| `Dignite.NotificationCenter.Domain` | Aggregates: `Notification`, `UserNotification`, `NotificationSubscription`, `PushDevice`. |
| `Dignite.NotificationCenter.Application` / `.Application.Contracts` | Inbox / subscription / push device app services + DTOs. |
| `Dignite.NotificationCenter.HttpApi` | REST controllers at `/api/notification-center`. |
| `Dignite.NotificationCenter.HttpApi.Client` | C# client proxies for remote consumers. |
| `Dignite.NotificationCenter.EntityFrameworkCore` | `INotificationStore` on EF Core (+ `NotificationCenterDbContext`). |
| `Dignite.NotificationCenter.MongoDB` | `INotificationStore` on MongoDB. |
| `Dignite.NotificationCenter.Web` | MVC UI: notification-bell view component + subscriptions page. |
| `Dignite.NotificationCenter.Push` | Serves the Push notifier's devices from the `PushDevice` registry. |
| `Dignite.NotificationCenter.Push.Identity` | Optional: stops pushing to a device once its ABP Identity login session has ended. |
| `notification-center` (Angular, `angular/projects/`) | Angular UI: proxy service + bell & subscriptions components. |

> Core never references the Notification Center — the two trees are independently installable, and
> Core + Distribution keep working with `NullNotificationStore` alone. The contracts stay in Core and their
> implementations live in Distribution, so a business module, the Notification Center's store and the Identity
> permission checker never depend on Distribution. The `host/` (runnable ABP MVC demo) and
> `angular/` (demo Angular app) folders are **local-dev demos only**; they are not packaged or
> published.

## Install

The commands below show all packages installed into one host project for clarity. In a layered ABP
solution, add each package to the matching layer and put the corresponding `[DependsOn]` entry in
that layer's module.

A business module references `Dignite.Abp.Notifications` only — definitions, routing and `INotificationPublisher`.
The host decides who implements the publisher, and the installation follows from where the inbox and the channels
live:

| Host | Installs | Publisher |
|---|---|---|
| **Monolith** — publishes, distributes and delivers in one process | `Distribution` + notifiers (+ the Notification Center for an inbox) | local (`DefaultNotificationPublisher`) |
| **Publisher** — its notifications are distributed by a notification service | `Remote` + `DefinitionStore.EntityFrameworkCore` | remote (`RemoteNotificationPublisher`) |
| **Notification service** — distributes for the publishers | `Distribution` + the Notification Center + notifiers + `DefinitionStore.EntityFrameworkCore` | local, plus the handler for remote publish requests |

A host with neither `Distribution` nor `Remote` has no `INotificationPublisher` and fails the first time one is
resolved; a host with both fails at startup.

### Monolith

Stateless forwarding — Distribution plus at least one external delivery channel. This example uses SignalR:

```bash
dotnet add path/to/MyApp.csproj package Dignite.Abp.Notifications.Distribution --prerelease
dotnet add path/to/MyApp.csproj package Dignite.Abp.Notifications.SignalR --version 10.0.0-rc.4
```

Email is optional:

```bash
dotnet add path/to/MyApp.csproj package Dignite.Abp.Notifications.Emailing --version 10.0.0-rc.4
dotnet add path/to/MyApp.csproj package Dignite.Abp.Notifications.Emailing.Identity --version 10.0.0-rc.4
```

Device push (iOS / Android) is optional too — the channel plus one provider:

```bash
dotnet add path/to/MyApp.csproj package Dignite.Abp.Notifications.Push.Expo
```

With the full Notification Center on EF Core — `Dignite.NotificationCenter.Application` brings Distribution with it:

```bash
dotnet add path/to/MyApp.csproj package Dignite.Abp.Notifications.SignalR --version 10.0.0-rc.4
dotnet add path/to/MyApp.csproj package Dignite.NotificationCenter.Application --version 10.0.0-rc.4
dotnet add path/to/MyApp.csproj package Dignite.NotificationCenter.HttpApi --version 10.0.0-rc.4
dotnet add path/to/MyApp.csproj package Dignite.NotificationCenter.EntityFrameworkCore --version 10.0.0-rc.4
dotnet add path/to/MyApp.csproj package Dignite.NotificationCenter.Web --version 10.0.0-rc.4
```

`Dignite.NotificationCenter.Web` is optional. For MongoDB, replace
`Dignite.NotificationCenter.EntityFrameworkCore` with
`Dignite.NotificationCenter.MongoDB`. Permission gating through `Dignite.Abp.Notifications.Identity` is also
optional; the host must be able to resolve ABP's `IUserRoleFinder` (see
[Recipient eligibility](#recipient-eligibility)).

### Publisher

A service whose notifications another process distributes installs Remote next to its business modules — no
notifier, no Notification Center — and the definition store, which saves its definitions where the notification
service reads them:

```bash
dotnet add path/to/MyService.csproj package Dignite.Abp.Notifications.Remote --prerelease
dotnet add path/to/MyService.csproj package Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore --prerelease
```

### Notification service

The process that hosts the inbox and the channels for the publishers installs the Notification Center (which
brings Distribution) and its channels, exactly like a monolith with an inbox, plus an event inbox so that a
redelivered publish request is processed once, and the definition store with its dynamic side on, so that it knows
the publishers' definitions:

```bash
dotnet add path/to/NotificationService.csproj package Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore --prerelease
```

See [Split deployment](#split-deployment) and [Definition catalog](#definition-catalog).

For an Angular host, install the version-matched UI library:

```bash
npm install @dignite/ng.notification-center@10.0.0-rc.4
```

Then follow [The two operation modes](#the-two-operation-modes) for the module dependencies and
[Defining and publishing a notification](#defining-and-publishing-a-notification) for the first
end-to-end notification.

## Upgrading from legacy 3.x

`10.x` is a from-scratch rewrite of the legacy `Dignite.Abp.Notifications*` and
`Dignite.NotificationCenter*` packages, not an in-place compatible upgrade. The major version
tracks the targeted ABP Framework major and also ensures that this implementation supersedes the
legacy `3.8.2` packages on NuGet.org.

Before changing an existing application from 3.x:

1. Treat the change as a module replacement and review every package/module dependency; legacy UI
   and notifier package names do not map one-for-one to this repository.
2. Give every custom `NotificationData` type a stable `[NotificationDataType("...")]` discriminator
   and register it through `NotificationDataOptions`.
3. Generate a new host-owned database migration for the three Notification Center aggregates. This
   repository does not ship a migration or an automated legacy-data converter.
4. Plan any migration of historical notifications and subscriptions explicitly before pointing the
   new module at a production database.
5. Regenerate or replace legacy clients with the new REST/C#/Angular clients and verify custom
   rendering and entity links.

Keep the application pinned to 3.8.2 until that migration has been tested; installing 10.x over a
legacy production database without a migration plan is unsupported.

## The two operation modes

### 1. Stateless forwarding — real-time push, no persistence

Install the distribution pipeline plus one or more notifiers. There is no inbox or subscription: you pass explicit
recipient `userIds`, the notifier pushes to connected clients, and nothing is stored
(`NullNotificationStore`).

```csharp
[DependsOn(
    typeof(AbpNotificationsDistributionModule),   // publisher, distributor, delivery handler (brings Core)
    typeof(AbpNotificationsSignalRModule)         // real-time channel
)]
public class MyHostModule : AbpModule { }
```

### 2. Full Notification Center — inbox, subscriptions, read/unread, REST API

Also install the Notification Center plus a persistence provider (EF Core or MongoDB). This adds a
persistent per-user inbox, subscriptions, read/unread state, and the `/api/notification-center` REST API.

```csharp
[DependsOn(
    typeof(AbpNotificationsDistributionModule),                // optional: NotificationCenterApplicationModule brings it
    typeof(AbpNotificationsSignalRModule),                     // real-time channel
    // typeof(AbpNotificationsEmailingModule),                  // optional: email channel
    // typeof(AbpNotificationsEmailingIdentityModule),          // optional: UserId -> Email via ABP Identity
    typeof(AbpNotificationsIdentityModule),                    // optional: permission gating
    typeof(NotificationCenterApplicationModule),            // inbox / subscription logic
    typeof(NotificationCenterHttpApiModule),                // REST API at /api/notification-center
    typeof(NotificationCenterEntityFrameworkCoreModule),    // persistence (or ...MongoDbModule)
    typeof(NotificationCenterWebModule)                     // optional: MVC bell + subscriptions UI
)]
public class MyHostModule : AbpModule { }
```

Fold the store into your host's own `DbContext` (this repo ships no `DbMigrator` — the host owns its
migrations):

```csharp
public class MyHostDbContext : AbpDbContext<MyHostDbContext>, INotificationCenterDbContext
{
    public DbSet<Notification> Notifications { get; set; } = default!;
    public DbSet<UserNotification> UserNotifications { get; set; } = default!;
    public DbSet<NotificationSubscription> NotificationSubscriptions { get; set; } = default!;
    public DbSet<NotificationDeliveryRecord> NotificationDeliveries { get; set; } = default!;

    public MyHostDbContext(DbContextOptions<MyHostDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureNotificationCenter();   // maps the four tables
    }
}
```

Then add a migration in your host and update the database, exactly as for any other ABP module.

### Upgrading subscription identity indexes

Subscription identity is the tuple `(TenantId, UserId, NotificationName, EntityTypeName?, EntityId?)`.
The two entity values are either both null (subscribe to every entity for that notification definition)
or both present (subscribe to exactly that entity). Notification Center persists three normalized,
non-null keys so this tuple is enforced with the same ordinal semantics in relational databases and
MongoDB: `TenantKey`, `NotificationNameKey`, and `ScopeKey`.

When upgrading an existing Notification Center database, create a host-owned migration/data migration
that:

1. adds the three keys as temporarily nullable;
2. backfills every subscription by calling `NotificationSubscriptionIdentity.GetTenantKey`,
   `GetNotificationNameKey`, and `GetScopeKey` with its existing natural values;
3. removes or repairs legacy rows with only one entity field and resolves any pre-existing duplicate
   identities before adding the unique index;
4. makes the keys required and replaces the old nullable natural-value indexes with the EF Core indexes
   from `ConfigureNotificationCenter`, or the equivalent MongoDB indexes declared by
   `NotificationCenterMongoDbContext.CreateModel`.

Do not add required keys with a shared empty-string default: existing rows would collide and the value
would not preserve ordinal identity. This repository intentionally ships no migration because the
consuming host owns its database and migration history. MongoDB consumers must likewise complete the
backfill before deploying the new unique index.

#### Delivery guarantees, batches, and partial progress

Distributing a notification writes the per-user inbox rows and publishes one `NotificationDeliveryRequestedEto` per
tenant/notification/user/channel. Those writes and outgoing event records commit together only when the host
enables ABP's transactional outbox. The process that actually hosts the selected channel consumes the work event
and calls the channel notifier once; processes that do not host that channel ignore the event. Delivery is
best-effort — there is no per-recipient delivery record, lease, or retry (see invariant §4) — so the inbox row is
the authoritative record. Atomic rollback of the inbox writes requires an ambient **transactional** ABP unit of
work; an outbox cannot make a non-transactional unit of work atomic.

| Setup | Persist + publish atomic | Failure/cancellation after a completed batch |
|---|---|---|
| EF Core, outbox + transactional UoW | yes, within one distributor/job invocation | the transaction rolls back inbox and outbox records |
| EF Core, no outbox + transactional UoW | no | inbox writes roll back, but an already published external event may have escaped |
| EF Core, non-transactional UoW | no | completed inbox batches and already published events can remain |
| MongoDB, outbox + transactional UoW on a supported topology | yes, within one distributor/job invocation | the transaction rolls back inbox and outbox records |
| MongoDB, no outbox or non-transactional UoW | no | completed inbox batches and already published events can remain |
| Stateless (Core + Distribution) channel consumer | no inbox | the channel event is fire-once; nothing is retained across process exit |

If you use the shipped `NotificationCenterDbContext`, one line enables both:

```csharp
Configure<AbpDistributedEventBusOptions>(options => options.UseNotificationCenterEfCoreOutbox());
```

If you folded the tables into your own `DbContext` as above, that extension points at the wrong
context. Implement the two marker interfaces, map the event tables, and route the outbox to your own
context instead:

```csharp
public class MyHostDbContext : AbpDbContext<MyHostDbContext>,
    INotificationCenterDbContext, IHasEventInbox, IHasEventOutbox
{
    public DbSet<IncomingEventRecord> IncomingEvents { get; set; } = default!;
    public DbSet<OutgoingEventRecord> OutgoingEvents { get; set; } = default!;
    // ...the four notification DbSets

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureEventInbox();
        builder.ConfigureEventOutbox();
        builder.ConfigureNotificationCenter();
    }
}

Configure<AbpDistributedEventBusOptions>(options =>
{
    options.Outboxes.Configure(config => config.UseDbContext<MyHostDbContext>());
    options.Inboxes.Configure(config => config.UseDbContext<MyHostDbContext>());
});
```

The shipped MongoDB context has an equivalent one-line opt-in; it configures both ABP event boxes:

```csharp
Configure<AbpDistributedEventBusOptions>(options => options.UseNotificationCenterMongoDbOutbox());
```

The transactional outbox requires a transaction-capable MongoDB deployment: a replica set running MongoDB 4.0 or
later with logical sessions. Standalone servers cannot commit the outbox transaction. The host must also use
transactional ABP units of work; do not set `AbpUnitOfWorkDefaultOptions.TransactionBehavior` to `Disabled`. For
production with more than one application instance, configure an ABP distributed-lock provider for the outbox
sender and inbox processor. (An earlier release shipped a startup self-probe that opened a throwaway transaction to
verify this; it was removed as over-engineering — it duplicated ABP/driver transaction-capability detection. Rely
on your deployment topology instead.)

#### MongoDB upgrade, collections, indexes, and cleanup

Existing MongoDB hosts should upgrade in this order:

1. convert the deployment to a MongoDB 4.0+ replica set and verify transactions independently;
2. ensure the application role can create indexes and read/write the ABP event-box collections;
3. inspect existing `AbpEventInbox` records and collapse duplicate non-empty `MessageId` values before the new
   unique index is created (retain the processed/discarded record when one exists, otherwise the oldest pending row);
4. deploy the new provider package and add `UseNotificationCenterMongoDbOutbox()`;
5. keep notification distribution inside transactional units of work, then monitor the ABP outbox/inbox workers
   before enabling traffic.

Notification business data needs no backfill and no collection is renamed. The provider uses ABP's conventional `AbpEventOutbox` and
`AbpEventInbox` names and creates indexes matching the EF Core query shapes: `CreationTime` for outbox sending,
`Status + CreationTime` for inbox processing/cleanup, and `MessageId` for duplicate detection. MongoDB makes
`MessageId` unique so two concurrent check-then-insert deliveries cannot create two handler-visible records. A
losing concurrent transaction is retried by the broker; ABP's normal existence check then observes the winner.
EF Core retains ABP's conventional non-unique `MessageId` index, which is a remaining provider difference.

**Breaking for custom context implementers:** `INotificationCenterMongoDbContext` now extends ABP's
`IHasEventInbox` and `IHasEventOutbox`. A consumer-owned implementation must expose
`IMongoCollection<IncomingEventRecord> IncomingEvents` and `IMongoCollection<OutgoingEventRecord> OutgoingEvents`,
call `ConfigureEventInbox()` and `ConfigureEventOutbox()` from `CreateModel`, and add the three indexes described
above (including MongoDB's unique `MessageId` index). The non-generic
`UseNotificationCenterMongoDbOutbox()` targets the shipped `NotificationCenterMongoDbContext`; a custom context
must instead configure both boxes explicitly:

```csharp
Configure<AbpDistributedEventBusOptions>(options =>
{
    options.Outboxes.Configure(config => config.UseMongoDbContext<MyHostMongoDbContext>());
    options.Inboxes.Configure(config => config.UseMongoDbContext<MyHostMongoDbContext>());
});
```

Ensure every resolved database is a transaction-capable replica set before accepting traffic.

Processed/discarded inbox records are retained for ABP's deduplication window and then removed by ABP's built-in
cleanup worker. Tune `AbpEventBusBoxesOptions.WaitTimeToDeleteProcessedInboxEvents` and
`CleanOldEventTimeIntervalSpan` for the host's redelivery window and storage budget. Outbox rows are deleted after
successful broker publication. Operational TTL indexes are not created because they can bypass ABP's status-aware
cleanup semantics.

EF Core requires host-owned schema migrations for `AbpEventOutbox`/`AbpEventInbox`; MongoDB creates collections
and indexes through its model initialization instead. Both providers use ABP's dispatcher/inbox terminology and
require a transactional unit of work for atomic persist-and-record. The MongoDB-specific unique inbox index and
replica-set startup probe are the remaining reliability/configuration differences. Neither provider claims
exactly-once external delivery or atomicity across all independently scheduled fan-out jobs.

Without the opt-in, a crash between inbox persistence and work-event publication can leave a notification with no
channel delivery. Cancellation remains a boundary for stopping new work, not compensation for completed work.

#### Retention and lifecycle cleanup

Notification Center **does not auto-expire anything**. A `UserNotification` inbox row lives until its owner deletes
it (the bell UI's per-row delete) or the host removes it; a `Notification` payload row lives until the host removes
it. This mirrors a device notification center — entries stay until the user swipes them away. Retention is not a
behavior the module schedules on the host's behalf.

Inbox growth (`publishes × recipients`) is real, but *when* to delete, *which tenant context* to delete in, and
*which instance* runs the scan in a cluster are host-infrastructure decisions a library cannot make correctly from
the inside. A host that needs time-based cleanup schedules it itself — an ABP `AsyncPeriodicBackgroundWorkerBase`, a
database agent job, whatever already fits its operations — honoring two rules the store relies on:

- **Never delete `Unread` rows.** Only aged `Read` inbox rows are safe to remove; deleting an unread row silently
  drops a notification and corrupts the bell's unread count.
- **Delete a `Notification` payload only once no inbox row references it.** The payload is shared across every
  recipient's inbox row, so an orphan check must precede its deletion.

Relational example, run per tenant context the host owns (`CurrentTenant.Change(tenantId)` for each tenant, plus the
host `null` scope), in bounded batches on a large store. Table names use the configurable
`NotificationCenterDbProperties.DbTablePrefix` (default `Abp`):

```sql
-- 1) Age out READ inbox rows; Unread (State = 0) rows are always retained.
DELETE FROM AbpUserNotifications
WHERE State = 1 AND CreationTime < @cutoff;

-- 2) Delete payloads no inbox row references any more.
DELETE FROM AbpNotifications n
WHERE n.CreationTime < @cutoff
  AND NOT EXISTS (SELECT 1 FROM AbpUserNotifications u WHERE u.NotificationId = n.Id);
```

Scope every statement by `TenantId`. On the MongoDB provider, prefer a two-step orphan check (collect unreferenced
payload ids, then delete) over a cross-collection join.

| Record | Owner | Deletion rule |
|---|---|---|
| `UserNotification` inbox row | Notification Center / current user | Users delete their own rows via the inbox. A host may additionally age out `Read` rows; `Unread` rows must be retained. All of a user's rows, whatever their state, go with [GDPR erasure](#personal-data-erasure-gdpr). |
| `Notification` base payload | Host retention job | Delete when older than the host's window **and** no inbox row still references it. [GDPR erasure](#personal-data-erasure-gdpr) leaves it to this job. |
| `NotificationSubscription` | User subscription settings | Not time-based. Delete only by the exact subscription identity through the subscription APIs — or all of a user's at once with [GDPR erasure](#personal-data-erasure-gdpr). |
| ABP event inbox/outbox records | ABP distributed event bus | Use ABP's status-aware event-box cleanup windows. Do not add TTL deletes that bypass processed/in-progress state. |

#### Personal data erasure (GDPR)

The Notification Center subscribes to ABP's `GdprUserDataDeletionRequestedEto` (`Volo.Abp.Gdpr.Abstractions`) and
erases what it keeps about that user: every inbox row (`UserNotification`, read and unread alike), every
`NotificationSubscription`, and every `PushDevice`. Nothing needs configuring; installing the Notification Center
registers the handler. ABP's open-source packages only define the event and nothing in this repository publishes
it, so your host needs a publisher — a GDPR module that raises it, or your own code through `IDistributedEventBus`.
With no publisher the handler never runs.

Two limits to know:

- **The shared `Notification` payload stays.** It has no owner and every recipient's inbox row references it, so
  it cannot be deleted per user. Its `Data` may still contain personal data (a name in a message, say); remove
  such payloads with the host retention job above, which deletes a payload once no inbox row references it.
- **The tenant comes from the user id.** Before ABP 10.7 the event carries only the user id, with no tenant, so
  the handler deletes by that id alone, in bulk — the user's rows go in every tenant sharing the database, and
  no other user's can match. A host with a database per tenant must make the event reach the right tenant's
  database (a distributed consumer runs in whatever tenant is ambient, usually the host). From ABP 10.7 the event
  names its tenant and the bus enters it; the handler needs no change.

Every delete is idempotent, so a redelivered event simply finishes the job. The deletes share one unit of work, but
that is atomic only where your host runs units of work in a transaction (not on a standalone MongoDB), so a
failure part-way leaves the erasure partly done until the event is delivered again: the ABP event inbox retries
a failed event, whereas a bus without an inbox hands the exception to the publisher. To change what is erased,
replace `GdprUserDataDeletionRequestedHandler` (its method is `virtual`) or add a further
`IDistributedEventHandler<GdprUserDataDeletionRequestedEto>` for your own data.

## Split deployment

A publisher does not have to host the inbox. A service that only publishes installs
`Dignite.Abp.Notifications.Remote`; a **notification service** — the one process that owns the inbox, the
subscriptions, the push devices and the channels — installs Distribution and the Notification Center. The business
modules are the same in both topologies: they reference `Dignite.Abp.Notifications` and call `INotificationPublisher`.

```
Publisher service (Core + Remote + DefinitionStore)
  business module ──► INotificationPublisher = RemoteNotificationPublisher
                        │ definition exists · channels resolved · payload serialized
                        ▼
              NotificationPublishRequestedEto ──► outbox ──► broker
  at startup: StaticNotificationDefinitionSaver ──► NotifDefinitionGroups / NotifDefinitions
                                                              │             (notification service's database)
Notification service (Distribution + Notification Center + notifiers + DefinitionStore)
  inbox ──► NotificationPublishRequestedHandler ──► local distributor (inline, or its own job queue)
              │ definition known? else throw → inbox retries   │ subscribers · eligibility · inbox rows
              └── DynamicNotificationDefinitionStore            ▼
                              NotificationDeliveryRequestedEto ──► SignalR / Email / Push
```

```csharp
// Publisher service
[DependsOn(
    typeof(MyBusinessApplicationModule),     // defines and publishes notifications, depends on AbpNotificationsModule
    typeof(AbpNotificationsRemoteModule),
    typeof(AbpNotificationsDefinitionStoreEntityFrameworkCoreModule)
)]
public class MyServiceModule : AbpModule { }

// Notification service
[DependsOn(
    typeof(AbpNotificationsSignalRModule),
    typeof(NotificationCenterApplicationModule),          // brings AbpNotificationsDistributionModule
    typeof(NotificationCenterHttpApiModule),
    typeof(NotificationCenterEntityFrameworkCoreModule),
    typeof(AbpNotificationsDefinitionStoreEntityFrameworkCoreModule)
)]
public class NotificationServiceModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<NotificationDefinitionStoreOptions>(options => options.IsDynamicNotificationStoreEnabled = true);
        // Requirements other services define are only known through ABP's own dynamic stores.
        Configure<PermissionManagementOptions>(options => options.IsDynamicPermissionStoreEnabled = true);
        Configure<FeatureManagementOptions>(options => options.IsDynamicFeatureStoreEnabled = true);
        // A publish request for a definition not read yet is retried with back-off (see Definition catalog).
        Configure<AbpEventBusBoxesOptions>(options =>
            options.InboxProcessorFailurePolicy = InboxProcessorFailurePolicy.RetryLater);
    }
}
```

**On the publisher**, `RemoteNotificationPublisher` does only what needs the publisher's process, and sends exactly
one `NotificationPublishRequestedEto` per notification, whatever the number of recipients:

1. The definition must exist locally; an undefined name throws while the caller is still on the line, as with the
   local publisher.
2. The payload is serialized once, with the stable discriminator, into `DataJson`.
3. The channels are resolved with the local `INotificationChannelResolver` — the routing rules (module defaults and
   host overrides in `NotificationRoutingOptions`) are configured in the publisher, so that is where they are read.
4. The event is published. Inside a unit of work it goes into the publisher's outbox, in the same transaction as the
   business change; enable ABP's transactional outbox on the publisher's database for that guarantee.

**On the notification service**, `NotificationPublishRequestedHandler` turns the event into a `NotificationInfo` with
the publisher's notification id, tenant, payload JSON and channels, and hands it to the same inline-or-job decision
the local publisher uses (`NotificationDistributionOptions.DirectDistributionUserThreshold`): a small explicit fan-out
is distributed inline, anything else — including subscription-resolved notifications — through the service's own
distribution job. Eligibility (`RequirePermission` / `RequireFeature`), the inbox rows and the delivery events all
happen there, in the notification's tenant. The channels the publisher resolved are used as they are; the service's
own routing is not consulted for them.

- **Idempotency.** Configure ABP's event inbox on the notification service: it deduplicates by message id and runs the
  handler, the inbox rows and the outbox records of the delivery events in one transaction. Without an inbox the
  handler opens its own unit of work. A notification id the store has already seen is not inserted again either.
- **Payloads.** The service stores and forwards `DataJson` as the publisher wrote it, so it needs no business payload
  types. The SignalR notifier pushes the JSON object unchanged, and the inbox REST API returns a payload type the service
  has not registered as the [tolerant placeholder](#reading-persisted-payloads-tolerant-reads) with the original JSON
  kept verbatim in `rawJson`. Register a payload type in the service only when its own Email or Push content providers
  must render it (`LocalizableMessageNotificationData` is registered by default).
- **Definitions.** Eligibility and the inbox's groups and display names read the service's
  `INotificationDefinitionManager`, which merges its own definitions with those the publishers saved to the
  [definition catalog](#definition-catalog). A publish request for a notification it does not know is refused (see
  below), never distributed without its requirements.
- **Queues.** Only a process with Distribution registers the distribution job — named
  `Dignite.Abp.Notifications.Distribute` (`[BackgroundJobName]`), independent of the CLR type — and only such a process
  handles `NotificationDeliveryRequestedEto` and `NotificationPublishRequestedEto`. A publisher with Remote consumes
  neither.

| Process | Situation | Result |
|---|---|---|
| Any | Neither Distribution nor Remote installed | `INotificationPublisher` has no implementation; the first resolution fails |
| Any | Both installed (the Notification Center's application layer counts as Distribution) | Startup fails, naming both packages |
| Publisher | A routing rule names an undefined notification | Startup fails (Core's check) |
| Publisher | A rule names a channel no notifier in the publisher hosts | Not checked — the channel is hosted elsewhere |
| Publisher | A definition resolves to no channel | Allowed: inbox-only |
| Publisher | Publishing an undefined notification | Throws, as the local publisher does |
| Notification service | A publish request names a notification neither defined here nor read from the catalog | The handler throws before writing anything; the event inbox retries it (use `InboxProcessorFailurePolicy.RetryLater`) |
| Notification service | `IsDynamicNotificationStoreEnabled` on, but ABP's `IsDynamicPermissionStoreEnabled` / `IsDynamicFeatureStoreEnabled` off | A startup warning per option: a requirement another service defines would filter every recipient out |

### Definition catalog

The notification service hosts no business module, so it has no definitions of its own. It gets them the way an ABP
microservice gets the permissions and features other services define: every process saves its static definitions to
shared tables at startup, and the service reads them. `Dignite.Abp.Notifications.DefinitionStore` is that store,
part for part after ABP's permission management domain (`StaticPermissionSaver`, `DynamicPermissionDefinitionStore`,
`PermissionDynamicInitializer`); `INotificationDefinitionManager` merges both sources, a definition this process
defines itself winning over a saved one of the same name.

- **Publishers save.** With the package installed, a publisher saves at startup, in the background with retries
  (`SaveStaticNotificationsToDatabase`, on by default). Map its `NotificationCenter` connection string to the
  notification service's database — the tables live next to the inbox. A publisher does not read the store
  (`IsDynamicNotificationStoreEnabled` is off by default) and validates its own publishes against its own definitions.
- **The notification service reads.** Turn on `NotificationDefinitionStoreOptions.IsDynamicNotificationStoreEnabled`
  there, and ABP's `PermissionManagementOptions.IsDynamicPermissionStoreEnabled` and
  `FeatureManagementOptions.IsDynamicFeatureStoreEnabled` too: a definition's `RequirePermission` /
  `RequireFeature` names another service's permission or feature, which ABP knows only through its dynamic stores —
  without them the check finds no such name and filters every recipient out, so the module warns at startup. The
  service reloads the catalog when a publisher's save changes it, at most 30 seconds later (the distributed cache
  holds a common stamp, as for ABP's permissions).
- **A notification not read yet is retried, not dropped.** A publisher may publish before the service has read its
  definitions. `NotificationPublishRequestedHandler` then throws before writing anything, and the event inbox retries
  it. Set `AbpEventBusBoxesOptions.InboxProcessorFailurePolicy = InboxProcessorFailurePolicy.RetryLater` on the
  service (back-off of 10·2ⁿ seconds, discarded after `InboxProcessorMaxRetryCount`, 10 by default); with ABP's
  default `Retry`, the same event is re-run every period and holds back the events behind it.
- **Several services share the tables.** A save never deletes a record just because this process no longer defines
  it — the record may be another service's. To remove one, list it on the service that defined it:
  `Configure<NotificationDefinitionStoreOptions>(o => o.DeletedNotifications.Add("Old.Name"))` (or
  `DeletedNotificationGroups` for a whole group). Two services must not define the same notification name.
- **One cache prefix, distinct application names.** The save is skipped when an MD5 hash of the definitions matches
  the one cached for the application (`IApplicationInfoAccessor.ApplicationName`); the hash, the stamp and the locks use
  ABP's distributed cache `KeyPrefix`. Every service of the deployment needs the same distributed cache and prefix, a
  distinct application name, and a real distributed lock (ABP's local lock only serializes one process).
- **Display texts by name.** Group and definition display texts are stored as `L:Resource,Key` (ABP's
  `ILocalizableStringSerializer`) and resolved in the service by resource name — from its own resources or, failing
  that, ABP's `IExternalLocalizationStore` (Language Management in a microservice solution). Attributes are stored
  only when their value is a JSON scalar (string, boolean, number, `Guid`, date/time).
- **Tables and migrations.** The package ships no migrations, like the Notification Center. The host that owns the
  database adds the tables to its migration DbContext and generates the migration:

  ```csharp
  protected override void OnModelCreating(ModelBuilder builder)
  {
      base.OnModelCreating(builder);
      builder.ConfigureNotificationCenter();
      builder.ConfigureNotificationDefinitionStore(); // NotifDefinitionGroups, NotifDefinitions
  }
  ```

  The tables are host-level (`[IgnoreMultiTenancy]`, skipped for a tenant-only database) and follow
  `NotificationDefinitionStoreDbProperties` (prefix `Notif`, the Notification Center's default; change both together).
  In a data migration environment (`AddDataMigrationEnvironment()`), the store neither saves nor reads.
- **MongoDB** has no implementation of the store yet; a `.MongoDB` package is a follow-up.

## Defining and publishing a notification

Most features need **no new entity** — `Notification` / `UserNotification` are generic containers for
any `NotificationData`.

**1. Define the payload** with a **stable discriminator** — never a CLR type name; this is what keeps
stored and remote JSON readable across assembly-version bumps:

```csharp
[NotificationDataType("Demo.OrderShipped")]
public class OrderShippedNotificationData : NotificationData
{
    public string OrderNumber { get; set; } = default!;
    public int ItemCount { get; set; }
}
```

> For a plain text message you don't need a subclass — use the built-in `MessageNotificationData`.

**2. Register the payload type** so it (de)serializes via its discriminator:

```csharp
Configure<NotificationDataOptions>(options =>
{
    options.Add<OrderShippedNotificationData>();
});
```

Discriminators use ordinal, case-sensitive comparison. Registering the same discriminator for two CLR
types, or the same CLR type under two discriminators, fails during application startup with both sides
named in the error. Repeating the exact same discriminator/type pair is safe and idempotent.

### Reading persisted payloads (tolerant reads)

The wire/storage envelope carries only the stable `type` discriminator — there is no schema-version field or
upcaster chain (an earlier design added event-sourcing-style versioning + N→N+1 upcasters; it was removed as
over-engineering, since notifications are read-once, not a replayable event stream). To change a payload's JSON
shape, prefer adding members: a newer payload of a *known* type reads leniently, with unknown members landing in
`ExtensionData`.

`INotificationDataSerializer.Deserialize(json)` is the single programmatic read boundary, and every read through
it is tolerant: an unknown discriminator or malformed known payload becomes `UnsupportedNotificationData`,
preserving the original discriminator and escaped raw JSON without activating an arbitrary CLR type, instead of
throwing. One bad historical row therefore cannot fail the rest of an inbox page. Notification Center inbox reads,
distributed-event deserialization, and HTTP server/client converters all go through this same tolerant path. The
payload is serialized once, by the publisher, and carried from there as that JSON (`NotificationInfo.DataJson`): the
store writes and returns it unchanged, every delivery event copies it, and only a reader that wants the typed view —
the inbox app service, a notifier — deserializes it. The
MVC and Angular libraries render the placeholder as a generic unsupported-notification message and do not display
its raw diagnostic JSON. Writing an unregistered CLR type still throws — that fail-fast is unconditional.

**3. Register the notification definition** through an `INotificationDefinitionProvider` — its group, name,
display text, and optional feature/permission gating. Like ABP permissions, every
definition belongs to a **group** (e.g. "Orders"), which is how the inbox and the subscription settings categorize
notifications:

```csharp
public class ShopNotificationDefinitionProvider : NotificationDefinitionProvider
{
    public override void Define(INotificationDefinitionContext context)
    {
        var orders = context.AddGroup("Demo.Orders", new FixedLocalizableString("Orders"));

        orders.AddNotification("Demo.OrderShipped", new FixedLocalizableString("Order shipped"));
    }
}
```

A definition says nothing about delivery channels — which external channels carry it is decided by
[routing](#routing), configured once in `NotificationRoutingOptions`.

Group and definition names use ordinal, case-sensitive comparison. Every duplicate group name and every duplicate
definition name — across all groups, not just within one — is a startup error, and the error identifies both
provider types; an equivalent-looking second definition is not treated as idempotent because definitions are
mutable after construction. To add definitions to a group another module created, use
`context.GetGroupOrNull(name)` instead of `AddGroup`. Provider types are convention-discovered across modules;
registering the same provider type more than once is idempotent and the provider executes once. Empty and
whitespace-only names are rejected immediately.

A group is definition-time metadata only: it is never persisted, so moving a definition to another group needs no
data migration. Stored notifications whose definition no longer exists fall into a synthetic "Other" inbox group
(`NotificationCenterConsts.OtherGroupName`), so they stay listable and deletable.

An explicitly empty `userIds` array remains a true no-op and returns before definition resolution.

**4. Publish** from your business code via `INotificationPublisher`:

```csharp
await _publisher.PublishAsync(
    "Demo.OrderShipped",
    new OrderShippedNotificationData { OrderNumber = "SO-1001", ItemCount = 3 },
    entityIdentifier: new NotificationEntityIdentifier("Demo.Order", "1001"),
    severity: NotificationSeverity.Success,
    userIds: new[] { customerId });
```

`PublishAsync` distributes small explicit fan-outs inline and larger ones via a background job (the
threshold is configurable — see [Configuration](#configuration)); with [Remote](#split-deployment) that decision is
made the same way by the process that distributes. Recipient semantics are deliberate:
`userIds: null` resolves **subscribers**, an empty array is an intentional no-op, and a non-empty array
targets those users explicitly. Duplicate explicit IDs are removed before the threshold is evaluated,
inbox rows are persisted, or channel delivery is published.

### Recipient eligibility

A definition's permission and feature requirements govern both who may subscribe **and who may receive** the
notification. The distributor applies the same `INotificationDefinitionManager.IsAvailableAsync` filter to both
subscription-derived and explicitly targeted candidates: caller-supplied exclusions are removed first, then each
remaining candidate must satisfy `PermissionName` and `FeatureName` (configured through `RequirePermission(...)` /
`RequireFeature(...)`) or is filtered out without an inbox row or channel event. An explicit `userIds` array is
**not** an authorization bypass. This is a delivery policy, not publisher authorization: publishing code still
needs its own application permission checks where appropriate.

Eligibility is evaluated in the notification's recorded `TenantId`, not whichever tenant happens to be ambient
when an inline call or background job executes. A tenant notification therefore uses that tenant's feature values
and permission context, while a host notification is evaluated in the host context. Recipient IDs are never logged.

Distribution's default `AlwaysGrantedNotificationPermissionChecker` grants everyone; `Dignite.Abp.Notifications.Identity`
replaces it with a real check. The package asks ABP's `IUserRoleFinder` (`Volo.Abp.Identity.Domain.Shared`) for the
recipient's role names, builds a principal carrying the user id, one role claim per role and, inside a tenant, the
tenant id (the claims ABP's user and role permission providers read), and passes it to ABP's `IPermissionChecker`.
It no longer depends on `Volo.Abp.Identity.Domain` or reads the Identity database, **but the host must be able to
resolve an `IUserRoleFinder`**:

- A process that maps the Identity database (a monolith) already has one: `Volo.Abp.Identity.Domain` registers
  `UserRoleFinder`.
- A notification service without that database installs an Identity `HttpApi.Client` package
  (`Volo.Abp.Identity.Pro.HttpApi.Client`, or the open-source `Volo.Abp.Identity.HttpApi.Client`) and points
  `RemoteServices:AbpIdentity` at the Identity service, which must expose its integration services
  (`AbpAspNetCoreMvcOptions.ExposeIntegrationServices`). Its `HttpClientUserRoleFinder` calls the integration
  service's `GetRoleNamesAsync` and is registered with `TryRegister`, so an in-process finder wins.
  The process also has to know the permission names other services define: enable
  `PermissionManagementOptions.IsDynamicPermissionStoreEnabled` (and
  `FeatureManagementOptions.IsDynamicFeatureStoreEnabled` for `RequireFeature(...)`), otherwise such a permission is
  silently denied.

Known limits: one role lookup per candidate recipient; a permission with state checkers is evaluated against the
ambient `ICurrentUser`, which is not logged in during background distribution; the user's `IsActive` flag is not
checked.

### Bounded recipient pipeline

Both explicit and subscription-derived recipients flow through the same bounded pipeline: resolve a candidate
batch → filter by definition eligibility → write inbox rows → publish one `NotificationDeliveryRequestedEto` per
eligible recipient/channel. The batch size is `NotificationDistributionOptions.RecipientBatchSize` (default 256;
must be between 1 and `MaxBatchSize` = 10,000, validated at host startup). The built-in subscription scan uses a
database-side distinct query with an exclusive user-ID keyset cursor rather than offset paging, so inserts/deletes
before the cursor cannot repeat or skip later recipients. `NullNotificationStore` implements the same contract
without persistence. All store operations accept a cancellation token, observed between candidate, persistence,
and delivery batches (not during a provider operation already in flight). Notification data must fit the chosen
transport's message-size limit.

`PublishAsync` distributes explicit fan-outs at or below `DirectDistributionUserThreshold` (default 5) inline, and
larger ones through a single background job carrying the caller's `Guid[]`; the job's distributor batches
recipients internally. `DirectDistributionUserThreshold` is capped by the same 10,000 safeguard.

`INotificationPublisher` records `CurrentTenant.Id` automatically. Code that calls `INotificationDistributor`
directly must populate `NotificationInfo.TenantId` for tenant notifications. That value is authoritative for
subscription lookup, eligibility, inbox persistence, and event/outbox publication; `null` explicitly means
**host**, even when the direct caller currently has an ambient tenant.

## Notifiers

A notifier implements the single canonical `INotificationNotifier` contract and relays one
`NotificationDeliveryRequestedEto` to a single channel. `Name` is the stable routing key. `DeliverAsync` receives a
`CancellationToken`; delivery is best-effort, so a notifier that intentionally skips a recipient simply returns,
and throwing is logged and dropped by the delivery handler (not retried). That distributed-event handler
(`NotificationDeliveryRequestedHandler`, in Distribution) is the transport adapter, so a channel plugin does not
implement an event-handler interface.

Each channel module maps its channel to its notifier type in `NotificationNotifierOptions`, and the handler
constructs only the notifier registered for a delivery's channel — an email delivery never builds the push notifier,
and one channel's notifier failing to construct does not affect the others. A channel name maps to one type (two
types claiming a channel fail the application start), so to customize a built-in channel replace its notifier type
in dependency injection — e.g. `[Dependency(ReplaceServices = true)]` plus `[ExposeServices(typeof(EmailNotifier))]`
on a subclass — rather than registering a second one. A custom channel registers itself the same way:

```csharp
Configure<NotificationNotifierOptions>(options =>
{
    options.Notifiers.Add<SmsNotifier>(SmsNotifier.ChannelName); // must equal SmsNotifier.Name
});
```

- **SignalR** — clients connect to the hub at `/signalr-hubs/notifications` (an ABP `AbpHub`, mapped
  **automatically**; the host must *not* call `MapHub`) and receive a flat `SignalRNotificationMessage`
  (`notificationId`, `notificationName`, `data`, `severity`, `creationTime`, `entityTypeName`, `entityId`) with the
  recipient list stripped, so siblings' user IDs never leak to each other. `data` is the raw discriminator-tagged
  JSON object (`{"type": "...", ...}`) — the same shape the REST inbox returns — because SignalR hub protocols
  serialize with their own options, not this module's polymorphic converter. The MessagePack hub protocol is not
  supported.
- **Push** — pushes to the recipient's phones. The channel is one (`"Push"`); the delivery service is
  chosen per device: each registered device names the `IPushProvider` that issued its token
  (`Dignite.Abp.Notifications.Push.Expo` ships the Expo Push Service provider), so a routing rule only ever
  names `"Push"`. Devices come from an `IPushDeviceStore`; the base package registers a null
  store, so nothing is sent (and a warning is logged) until a real one replaces it — with the Notification
  Center, install `Dignite.NotificationCenter.Push` to serve devices from its `PushDevice` registry (see
  [Push devices](#push-devices-notification-center)); without it, implement the store over your own device
  storage. Content is built once
  per device culture by an `INotificationPushContentProvider` chain that mirrors the email one (built-in
  fallbacks for `MessageNotificationData` and `LocalizableMessageNotificationData`); every message also
  carries `notificationId`, `notificationName`, `entityTypeName` and `entityId` as silent data so a tapped
  notification can open — and mark read — its inbox entry. A device a provider reports dead is removed
  from the store. Push text travels through Apple's, Google's and the provider's servers and shows on a
  lock screen: keep it to "something new, open the app". With Expo, turn on *Enhanced Security for Push
  Notifications* and set `ExpoPushOptions.AccessToken` in production — without it, anyone holding a
  device's Expo push token can push to it. Expo push *receipts* are not polled (no delivery state, by
  design); only dead devices reported on the ticket are removed.
- **Emailing** — resolves each recipient's email address and sends via ABP's `IEmailSender`. Addresses
  come from an ordered `IEmailNotificationAddressResolver` chain: `EmailNotifier` takes the first
  non-null address result. The result may also carry a recipient culture used while building that user's email.
  The base Emailing package registers no resolver, so nothing is sent (and a warning
  is logged) until one exists. Install `Dignite.Abp.Notifications.Emailing.Identity` to get the account
  email as the built-in fallback, and register your own resolver at
  `NotificationEmailProviderOrders.Default` to claim specific notifications — for example, sending an
  *order shipped* mail to the contact address recorded on the order rather than the account address:

  ```csharp
  public class OrderEmailNotificationAddressResolver
      : IEmailNotificationAddressResolver, ITransientDependency
  {
      public int Order => NotificationEmailProviderOrders.Default;

      public async Task<EmailNotificationAddress?> GetEmailOrNullAsync(
          EmailNotificationAddressResolveContext context,
          CancellationToken cancellationToken = default)
      {
          if (context.Notification.NotificationName != "Demo.OrderShipped")
          {
              return null;  // not mine — fall through to the Identity fallback
          }

          var contact = await _orders.FindContactAsync(
              context.Notification.EntityId!, context.UserId, cancellationToken);
          return contact == null
              ? null   // no address — fall through
              : EmailNotificationAddress.To(contact.Email, contact.CultureName);
      }
  }
  ```

  A resolver returns the address **for this user** in this entity context, never "the entity's
  address" — `EmailNotifier` builds the body for that same user and sends one email per recipient.
  The optional `CultureName` on `EmailNotificationAddress` selects the culture for that recipient's content build;
  when it is omitted, `NotificationEmailOptions.DefaultCulture` is used. Culture is scoped only around one
  recipient's content build and is restored before the next recipient is processed. When ABP Setting Management is
  installed, the Identity integration uses its user-targeted setting manager and fallback chain; otherwise the
  configured `NotificationEmailOptions.DefaultCulture` is used.
  Never call `CurrentTenant.Change` in a resolver: ABP's event bus has already entered the
  notification's tenant. `context.TenantId` exists for resolvers that must forward the tenant across a
  boundary the ambient scope cannot cross, such as a remote user service. The host still owns SMTP /
  `IEmailSender` configuration.

  The **body** comes from the parallel `INotificationEmailContentProvider` chain. Derive from
  `NotificationEmailContentProvider<TData>` and the payload is narrowed for you — forgetting the type
  check would otherwise make your provider claim every notification in the system:

  ```csharp
  public class OrderShippedEmailContentProvider
      : NotificationEmailContentProvider<OrderShippedNotificationData>, ITransientDependency
  {
      protected override Task<NotificationEmail?> BuildOrNullAsync(
          NotificationEmailBuildContext context,
          OrderShippedNotificationData data,
          CancellationToken cancellationToken)
      {
          return Task.FromResult<NotificationEmail?>(
              new NotificationEmail($"Order {data.OrderNumber} shipped", RenderBody(data), isBodyHtml: true));
      }
  }
  ```

  Subclasses of `TData` match too, so a provider typed on a base payload keeps handling payloads derived
  from it. Implement `INotificationEmailContentProvider` directly only for the rare provider that handles
  two unrelated payload types.

**Write your own** (Web Push, FCM, SMS, Webhook, …): create a project depending on
`Dignite.Abp.Notifications.Abstractions` **only**, and handle the event:

```csharp
public class WebPushNotifier
    : INotificationNotifier, ITransientDependency
{
    public const string ChannelName = "WebPush";
    public string Name => ChannelName;

    public async Task DeliverAsync(
        NotificationDeliveryRequestedEto request,
        CancellationToken cancellationToken = default)
    {
        // Typed, in-process: render from the hydrated payload...
        var payload = NotificationPayload.FromRequest(request, _dataSerializer);
        var body = payload.Data is MessageNotificationData message ? message.Message : payload.NotificationName;

        // ...but put only a flat, serializer-agnostic DTO on the wire (see below).
        await _webPush.SendAsync(
            request.UserId,
            new WebPushMessage { NotificationId = request.NotificationId, Body = body, DataJson = request.DataJson },
            cancellationToken);
    }
}
```

`DeliverAsync` handles one recipient/channel request and observes cancellation. Delivery is best-effort: to skip a
recipient (e.g. no address), simply return; throwing is logged and dropped by the delivery handler, not retried.

The event carries the payload pre-serialized as discriminator-tagged JSON (`request.DataJson`) so it survives any
transport serializer — ABP's event bus (outbox/inbox included) serializes ETOs with plain `System.Text.Json`, without
the application's JSON options. Inject `INotificationDataSerializer` (it ships with Abstractions) and hydrate through
`NotificationPayload.FromRequest`; the read is tolerant, so an unknown or malformed payload becomes
`UnsupportedNotificationData` instead of throwing.

`NotificationPayload` is an in-process view for rendering. A notifier that serializes a notification for an
external client must not put it on the wire: define its own flat DTO carrying the data as raw JSON, as the SignalR
package does with `SignalRNotificationMessage`.

### Routing

Which external channels carry a notification is **not** part of its definition: the module that defines a
notification does not know what the host has installed. Routing lives in `NotificationRoutingOptions` and has two
levels only — a rule per notification name, and a `Default` for notifications without a rule. Core knows no channel
names; they are the open set registered through `NotificationNotifierOptions` (each notifier package keeps its own
`XxxNotifier.ChannelName`, or just write the string).

A module adds defaults for its own notifications in its `ConfigureServices`; the host module, loaded last,
configures the same options again to override any of them. Later writes win, and a repeated rule for the same
notification replaces the earlier one as a whole (channels are not merged):

```csharp
// Business module:
Configure<NotificationRoutingOptions>(options =>
{
    options.ForNotifications(
        ["Demo.OrderShipped", "Demo.OrderCancelled"],
        SignalRNotifier.ChannelName, "Push");
});

// Host module (optional):
Configure<NotificationRoutingOptions>(options =>
{
    options.Default = [SignalRNotifier.ChannelName];                     // notifications without a rule
    options.ForNotification("Demo.OrderCancelled", SignalRNotifier.ChannelName, EmailNotifier.ChannelName);
    options.InboxOnly("Demo.AuditLog");                                  // explicit: persisted, no external channel
});
```

- `ForNotification(name, channels...)` needs at least one channel; `InboxOnly(names...)` is the explicit rule for
  "no external channel" and overrides `Default`. Channel names are trimmed and de-duplicated ignoring case;
  notification names are case-sensitive.
- A notification with no rule and no `Default` is inbox-only: it is persisted for the recipient but no notifier
  event is published. `Default` is never "every installed channel", so adding a notifier package cannot silently
  fan existing notifications out to a new channel.
- Bind from configuration with the standard options binding
  (`Configure<NotificationRoutingOptions>(configuration.GetSection(...))`); dictionary keys are notification names.
- For routing that depends on the tenant, severity or a setting, replace `INotificationChannelResolver`. It is called
  once per notification, before recipients are batched, so it cannot express per-user preferences.

Startup checks run once the definitions are materialized, against the process's own definitions (not those read from
the [definition catalog](#definition-catalog)). The first runs wherever Core is installed; the other two
are about the process that delivers, so they run only where Distribution is installed — a [remote
publisher](#split-deployment) names channels another process hosts, and an inbox-only notification is fine there:

| Situation | Checked by | Result |
|---|---|---|
| A rule names a notification that is not defined | Core | Startup fails, listing every unknown name |
| A rule or `Default` names a channel no notifier in this process hosts | Distribution | Warning per channel, listing the notifications that use it; set `RequireHostedChannels = true` to fail instead |
| Stateless mode (`NullNotificationStore`) and a definition resolves to no channel | Distribution | Startup fails, listing the notifications (skipped if `INotificationChannelResolver` is replaced) |

An unhosted channel is legitimate in a split deployment, where another process delivers it. At runtime the processes
that do not host the channel ignore its delivery events (logged at Debug only; the startup warning is the one place
a misconfiguration is reported).

In stateless forwarding mode an inbox-only notification has nowhere to persist; the checks above reject it at startup
(and the distributor rejects it at publish time when a replacement resolver returns no channel). Give every
notification a rule or a `Default` in that mode.

## Push devices (Notification Center)

The Notification Center keeps a registry of the phones each user can be pushed to — one `PushDevice` per
app installation, identified by the token its push provider issued. Install `Dignite.NotificationCenter.Push`
(next to `Dignite.Abp.Notifications.Push` and a provider such as `.Push.Expo`) and the Push notifier reads
devices from it:

```csharp
[DependsOn(
    typeof(AbpNotificationsPushExpoModule),        // the Push channel + the Expo provider
    typeof(NotificationCenterPushModule)           // devices from the PushDevice registry
    // typeof(NotificationCenterPushIdentityModule) // optional: push follows the ABP login session
)]
public class MyHostModule : AbpModule { }
```

```csharp
Configure<ExpoPushOptions>(options =>
{
    options.AccessToken = configuration["Expo:AccessToken"]; // secret configuration, never source control
});
```

The app drives the registry through two calls (see [REST API](#rest-api-notification-center)):

- **Register** on every launch, after sign-in, and when the user changes the app language. The device's
  language is the request culture — send `Accept-Language` — and its login session the caller's `session_id`
  claim, if the host issues one. A token already registered to someone else, in any tenant, moves to the
  caller: whoever signed in last on a phone is the one it gets pushes for.
- **Unregister** at sign-out, *before* revoking the access token — the call needs it. It only ever removes
  the caller's own device.

A user keeps at most `PushDeviceOptions.MaxDevicesPerUser` devices (default 10); registering one more forgets
the device seen least recently. Together with dead-device reports from the provider this bounds the registry —
there is no cleanup worker. The token key is unique across tenants, so a phone moving to another tenant stops
receiving the previous tenant's pushes; with a database per tenant that cannot be enforced across databases,
and the guarantee rests on unregistering at sign-out. When a user's data is erased on request, their devices go
with it — see [Personal data erasure](#personal-data-erasure-gdpr).

`Dignite.NotificationCenter.Push.Identity` makes push follow the ABP login session: a device registered
under a session that no longer exists (signed out, revoked, ended by the concurrent-login rule, cleaned up as
inactive) is forgotten instead of pushed to — which also covers sign-outs the app could not report. It needs a
host that maintains `IdentitySession` rows and issues the `session_id` claim, i.e. ABP Identity Pro's session
management; elsewhere devices carry no session and the package does nothing.

## REST API (Notification Center)

Three controllers expose the current user's notification center under `/api/notification-center`:
`UserNotificationController` (the inbox), `NotificationSubscriptionController` (subscriptions) and
`PushDeviceController` (push devices).

| Method & route | Purpose |
|---|---|
| `GET /api/notification-center/notifications` | List the caller's notifications (paged; filter by state / date / `groupName`) |
| `GET /api/notification-center/notifications/unread-count` | Unread notification count (for the bell badge) |
| `GET /api/notification-center/notifications/groups` | The caller's inbox groups with per-group unread counts (for inbox tabs) |
| `POST /api/notification-center/notifications/{id}/mark-as-read` | Mark one notification read |
| `POST /api/notification-center/notifications/mark-all-as-read` | Mark all read |
| `DELETE /api/notification-center/notifications/{id}` | Delete one |
| `DELETE /api/notification-center/notifications/read` | Delete all **read** notifications (unread are preserved) |
| `GET /api/notification-center/subscriptions` | List the caller's subscriptions |
| `POST /api/notification-center/subscriptions` | Subscribe to the definition-wide or exact entity scope in the JSON body |
| `DELETE /api/notification-center/subscriptions` | Unsubscribe only the definition-wide or exact entity scope in the query |
| `POST /api/notification-center/push-devices/register` | Register (or refresh) the caller's device: `{ provider, token }` in the body |
| `POST /api/notification-center/push-devices/unregister` | Forget the caller's device: `{ provider, token }` in the body |

All endpoints are scoped to the authenticated caller. Use `...HttpApi.Client` for a typed C# proxy, or the
ABP-generated Angular services.

`GET notifications/groups` lists groups in definition order. A group appears when the caller can currently receive
one of its definitions or still has unread notifications in it; the "Other" group appears only while the caller has
notifications whose definition no longer exists. Each `UserNotificationDto` and `NotificationSubscriptionDto` also
carries its `groupName` and localized `groupDisplayName`. The per-group unread counts come from one grouped query over
the caller's unread rows (plus one count for "Other" when it has no unread rows), so this endpoint is meant for the
inbox page; the bell badge keeps using `unread-count`.

The subscribe/unsubscribe request contains `notificationName` plus optional `entityTypeName` and `entityId`; the
two entity fields must be supplied together (a definition-wide subscription omits both). `GET subscriptions`
returns the definition-wide row for each available definition and every persisted entity-specific row separately,
so clients must use the full three-field scope rather than infer state from a flattened notification name.

For subscription-driven distribution, a notification without an entity matches only definition-wide
subscriptions. A notification for a concrete entity matches the union of definition-wide subscriptions
and that exact entity scope; a user present in both receives one inbox row and one channel delivery.

### Pre-stable application/domain API migration

This pre-stable revision moves the REST surface under the `/api/notification-center` prefix and splits the
user-facing service/controller into an inbox half and a subscription half. Recompile consuming code, update
hard-coded URLs, and regenerate ABP clients (JS / Angular proxies) after applying these source-level changes:

| Before | Now | Consumer action |
|---|---|---|
| `NotificationsController` @ `/api/notifications` | `UserNotificationController` @ `/api/notification-center/notifications` + `NotificationSubscriptionController` @ `/api/notification-center/subscriptions` | Update hard-coded URLs; regenerate JS / Angular proxies. |
| `IUserNotificationAppService` (inbox + subscriptions) | `IUserNotificationAppService` (inbox) + `INotificationSubscriptionAppService` (subscriptions) | Inject the subscription service for `GetSubscriptions` / `Subscribe` / `Unsubscribe`. |
| `SubscribeScopedAsync(dto)` / `UnsubscribeScopedAsync(dto)` (+ the name-only `…Async(string)` overloads) | `SubscribeAsync(dto)` / `UnsubscribeAsync(dto)` | One method each; a definition-wide subscription leaves both entity fields null. |
| `INotificationAppService` / `NotificationAppService` | `IUserNotificationAppService` / `UserNotificationAppService` | Rename injected service types and replacements. |
| `GetCountAsync` (count by state) | `GetUnreadCountAsync()` (no parameter) | Now unread-only, for the bell badge; get other counts from `GetListAsync`'s `TotalCount`. Route: `GET /api/notification-center/notifications/unread-count`. |
| `DeleteAllAsync(state?)` | `DeleteAllReadAsync()` (no parameter) | Bulk delete now removes only read notifications; delete unread ones individually via `DeleteAsync`. Route: `DELETE /api/notification-center/notifications/read`. |
| `IUserNotificationManager` / `UserNotificationManager` | removed | Use `INotificationStore` for inbox queries and state mutations. |
| `INotificationSubscriptionManager` | concrete `NotificationSubscriptionManager` | Use the manager only for validated subscription mutations; use `INotificationStore` for reads. |
| Subscription-manager read methods | removed | Use `INotificationStore.GetSubscriptionsAsync` / `IsSubscribedAsync` directly in query paths. |
| `INotificationRetentionCleanupService` / `NotificationRetentionCleanupService` / `NotificationRetentionManager` | removed | Schedule inbox/payload cleanup in the host — see "Retention and lifecycle cleanup". |

`INotificationDefinitionManager` is asynchronous (`GetAsync`, `GetOrNullAsync`, `GetAllAsync`, `GetGroupsAsync`,
`GetGroupOrNullAsync`) because it merges the [definition catalog](#definition-catalog); code that called the
synchronous methods awaits these instead. It remains replaceable for a custom availability policy; a custom definition
registry replaces `IStaticNotificationDefinitionStore` (override `StaticNotificationDefinitionStore.CreateGroups`),
which startup validates. `INotificationStore` likewise remains a genuine extension boundary.

## UI libraries (optional)

- **MVC** (`Dignite.NotificationCenter.Web`): a notification-bell view component, a subscriptions settings tab,
  and an inbox page at `/NotificationCenter/Notifications` (`NotificationCenterWebConsts.InboxPageUrl`). Configure
  the hub URL and per-type rendering via `NotificationCenterWebOptions` — `SignalRHubUrl`, `DataViewComponents`
  (keyed by discriminator), and `EntityLinkResolvers`; the inbox page renders items with the same options.
- **Angular** (`angular/projects/notification-center`): an ABP-generated proxy service plus bell, subscriptions and
  inbox components, built against `/api/notification-center` and the SignalR hub. Mount the inbox with
  `createRoutes()` at `/notifications`.

The inbox page lists the user's notifications under group tabs with unread counts, with an all/unread filter,
paging, per-item delete, "mark all as read" and "clear read". Clicking an item marks it read and follows its entity
link; the item stays listed until the user deletes it. Neither UI adds a main-menu item for it — the page is reached
from the bell.

Both bells open a SignalR connection to `/signalr-hubs/notifications` and refresh from the REST inbox when a
`ReceiveNotification` message arrives or the connection reconnects (auto-reconnect handled by the SignalR client);
the REST inbox is always the authoritative source, since SignalR does not replay missed notifications. If the
SignalR notifier isn't installed or `@microsoft/signalr` isn't loaded, the bell degrades to a non-live view. MVC
reads the hub URL from `NotificationCenterWebOptions.SignalRHubUrl`; for remote deployments point that (or the
Angular API URL) at the externally reachable hub while keeping `/api/notification-center/notifications` as the inbox source.

## Configuration

```csharp
Configure<NotificationDefinitionRegistration>(options =>
{
    // Provider types are normally convention-discovered; explicit registration is also supported.
    options.DefinitionProviders.Add(typeof(MyNotificationDefinitionProvider));
});

// Distribution only: configured in the process that distributes (a notification service, for remote publishers).
Configure<NotificationDistributionOptions>(options =>
{
    // Explicit recipients above this count distribute on a background job instead of inline. Default: 5.
    options.DirectDistributionUserThreshold = 10;

    // Recipients resolved, persisted, and published per batch. Between 1 and MaxBatchSize (10,000). Default: 256.
    options.RecipientBatchSize = 256;
});

Configure<NotificationEmailOptions>(options =>
{
    // Used when an email address resolver does not supply a recipient culture.
    options.DefaultCulture = "en-US";
});

// EF Core Notification Center hosts can opt in to ABP's transactional outbox so the persisted
// inbox rows and NotificationDeliveryRequestedEto outbox records commit together.
Configure<AbpDistributedEventBusOptions>(options =>
{
    options.UseNotificationCenterEfCoreOutbox();
});

// MongoDB hosts use the equivalent opt-in on a transaction-capable MongoDB 4.0+ replica set with
// transactional ABP units of work.
Configure<AbpDistributedEventBusOptions>(options =>
{
    options.UseNotificationCenterMongoDbOutbox();
});
```

## Architecture

```
Notifications.Abstractions   ── data model + NotificationDeliveryRequestedEto + NotificationPublishRequestedEto
        │                       + notifier contract
Notifications (Core)         ── definitions · routing · contracts (publisher, store, distributor, permission checker)
        │
   ┌────┴──────────────────────────────┐
Notifications.Remote               Notifications.Distribution
publish → NotificationPublish-     publish → distribute → publish delivery events (best-effort);
RequestedEto (another process      handles NotificationPublishRequestedEto from remote publishers
distributes)                              │
                                 ┌────────┴───────────────┐
                              Notifiers                 NotificationCenter (optional)
                              (SignalR / Email / …)     inbox · subscriptions · REST API · UI
                                                        (EF Core / MongoDB)
```

**Publish → distribute → notify:**

1. Business code calls `INotificationPublisher.PublishAsync(...)`. The payload is serialized there, once, into
   `NotificationInfo.DataJson`; from then on it travels as that string.
2. With Distribution, small explicit fan-outs distribute inline and larger ones enqueue a
   `NotificationDistributionJob`. With Remote, one `NotificationPublishRequestedEto` carries the notification (and the
   channels resolved in the publisher) to the process that distributes, which makes the same inline-or-job decision.
3. The distributor resolves bounded recipient batches (explicit `userIds`, or subscribers from
   `INotificationStore`), checks the definition's feature/permission availability, persists bounded
   inbox groups (a no-op under `NullNotificationStore`), then publishes one `NotificationDeliveryRequestedEto`
   per recipient/channel when external channels are configured.
4. Only a process hosting the selected channel handles the work: Distribution's event handler resolves the channel
   notifier and calls `DeliverAsync` once. Delivery is best-effort — a channel that throws is logged and dropped,
   not retried; the inbox row is the authoritative record.

`NotificationDeliveryRequestedEto` is the load-bearing boundary between scheduling and delivery and the extension
point for any new channel; `NotificationPublishRequestedEto` is the boundary between a publisher and the process that
distributes for it. Neither carries a live `NotificationData`. Under either Notification Center persistence provider, hosts should opt in to ABP's
transactional outbox (see [Configuration](#configuration)) so notification, inbox, and outgoing work records
commit together.

> **Serialization invariant:** every `NotificationData` subclass must carry a stable
> `[NotificationDataType]` discriminator and round-trip through System.Text.Json only — never a CLR type name /
> `AssemblyQualifiedName`, never Newtonsoft. The envelope carries only that discriminator (no schema version, no
> upcaster chain); a newer payload of a known type reads leniently. That is what keeps historical and remote
> payloads readable and lets non-.NET clients render them.

## Build & test

Run these from this `notifications/` directory. To build both modules in this repository at once,
use the aggregate `Dignite.Abp.Modules.slnx` at the repository root instead.

```bash
# Build / test everything (core + notification-center) from the one solution
dotnet build Dignite.NotificationCenter.slnx
dotnet test  Dignite.NotificationCenter.slnx     # starts an embedded mongod for the MongoDB provider tests

# Core only (skips the embedded mongod):
dotnet test core/test/Dignite.Abp.Notifications.Tests

# Pack for local testing (version / license come from the repository root Directory.Build.props)
dotnet pack Dignite.NotificationCenter.slnx -c Release
```

EF Core integration tests run on in-memory Sqlite and MongoDB tests on an embedded mongod, so
`dotnet test` needs no database install and no migration step.

**Run the demo host** — a runnable ABP MVC host wiring the whole stack (SignalR + Identity + EF Core
+ MVC UI) end-to-end, with a demo notification type and a publish button:

```bash
dotnet run --project host/Dignite.NotificationCenter.Web.Host
```

The `angular/` workspace consumes the same API for the Angular demo.

### Host secrets

`host/Dignite.NotificationCenter.Web.Host/appsettings.json` deliberately contains **no** certificate or
encryption passphrases. In Development the host uses ABP's development signing certificate and needs
nothing configured. Outside Development it requires these values and fails fast at startup if they are
missing — supply them with .NET user-secrets, environment variables, or a secret store:

```text
AuthServer:CertificatePassPhrase
StringEncryption:DefaultPassPhrase
```

For Docker or other deployments, use the corresponding double-underscore environment variable names
(for example, `AuthServer__CertificatePassPhrase`).

## Repository layout

```
core/                 core framework (Abstractions, Notifications, Distribution, Remote, Identity, Emailing, Emailing.Identity, SignalR, Push, Push.Expo) + tests
notification-center/  optional persistence + REST API + MVC UI + tests (EF Core & MongoDB)
angular/              Angular UI library (projects/notification-center) + demo app   ── local dev only
host/                 runnable ABP MVC demo host                                     ── local dev only
Dignite.NotificationCenter.slnx   one solution aggregating core/ + notification-center/
```

## License

Licensed under [LGPL-3.0-only](../LICENSE).
