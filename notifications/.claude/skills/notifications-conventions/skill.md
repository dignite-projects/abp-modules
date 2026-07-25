---
name: notifications-conventions
description: How the Dignite.Abp.Notifications module applies ABP — the three BasicAggregateRoot aggregates with no custom repository interfaces, INotificationStore as the query seam, hand-written MapToDto (no Mapperly/AutoMapper), explicit HttpApi controllers, the two-layer authorization model with INotificationPermissionChecker, the NotificationDeliveryRequestedEto distributed event, the distribution background job, tenant handling, and read-time localization. Read when writing or reviewing code under notifications/ and the generic abp-* skill doesn't say what THIS module does.
---

# notifications — Module Conventions

> This is the "how *this* module does it" layer. Generic ABP conventions live in the repo-root `abp-*` skills
> (`abp-core`, `abp-ddd`, `abp-application-layer`, `abp-authorization`, `abp-multi-tenancy`,
> `abp-infrastructure`, `abp-ef-core`, `abp-mongodb`, `abp-testing`). The **hard invariants** — the things a
> change must not break — are in the `notifications-invariants` skill; structure and the "add a feature" flow
> are in [`notifications/CLAUDE.md`](../../../CLAUDE.md).
>
> Where this file and a generic `abp-*` skill disagree, **this file wins for code under `notifications/`**.
> Note this module deliberately differs from `file-storing` on repositories, object mapping, controllers, and
> distributed-event posture — don't cross-apply the other module's conventions.

## The three aggregates deviate from the generic template — on purpose

`Notification`, `UserNotification`, and `NotificationSubscription` deviate in ways that are **intentional —
follow them, don't "fix" them back to the generic pattern**:

- They inherit `BasicAggregateRoot<Guid>` (not `AggregateRoot<Guid>` or `AuditedAggregateRoot<Guid>`) and
  implement `IMultiTenant` **explicitly** (`public virtual Guid? TenantId { get; protected set; }`) rather than
  relying on a richer built-in base class — `CreationTime` is likewise a plain modeled property, not ABP's
  audited-entity convention. `BasicAggregateRoot` is leaner: no local-event collection overhead.
- Setters are `protected` (not `private`), since these entities live in the same project/assembly as the code
  that constructs them. The pattern is otherwise the same: constructor + behavior method (e.g.
  `UserNotification.SetState(...)`) rather than public setters.

### Multi-tenancy on the aggregates

`NotificationStore` passes the tenant through each entity's constructor
(`notification.TenantId ?? CurrentTenant.Id`).

**Tenant scope is authoritative during distribution.** Evaluate a notification in its **recorded `TenantId`**;
do not inherit an unrelated ambient tenant from an inline caller or background worker. Host (`null`) and tenant
contexts must never mix — `null` is *authoritative host*, not an instruction to fall back to ambient state.
Direct `INotificationDistributor` callers must populate tenant notifications explicitly. Subscription
uniqueness includes the tenant and the complete scope (`notifications-invariants` §6, §7, §8).

## No custom repository interfaces — `INotificationStore` is the seam

All querying (including multi-field filters like "this user's unread notifications since date X") is written
directly against the generic `IRepository<T, Guid>` **inside the `INotificationStore` implementation**. New
queries go on `INotificationStore`, not behind a new repository interface.

Only reach for a custom repository interface for a genuinely new aggregate that needs the same query from
multiple call sites.

```csharp
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(INotificationStore))]
public class NotificationStore : INotificationStore, ITransientDependency { }
```

This replaces Core's `NullNotificationStore` once `NotificationCenter` is installed. `NullNotificationStore`
must implement the **complete** contract without persistence — including keyset paging and bounded
multi-insert.

## Base classes and DI lifetimes

| Base class | Used by |
|---|---|
| `BasicAggregateRoot<TKey>` | All three `NotificationCenter.Domain` entities |
| Domain services (plain) | `NotificationDefinitionManager`, `UserNotificationManager`, `NotificationSubscriptionManager` |

`NotificationStore : INotificationStore, ITransientDependency` inherits **no** ABP base class, so it correctly
**injects** `IClock` / `IGuidGenerator` / `ICurrentTenant` via its constructor rather than using inherited
properties.

Definition/registry caches (name → definition lookups) are fine as singletons — the permission/store checks
that ride along with them are not. Check `notifications-invariants` §2 before marking any manager
`ISingletonDependency`; that exact mistake is this module's motivating bug.

## Application layer

### No mapper — mapping is hand-written

`NotificationAppService` does **not** use Mapperly or AutoMapper — mapping is a hand-written
`protected virtual TDto MapToDto(...)` method on the AppService itself. Follow this unless the DTO surface
grows enough to justify a mapper. (This is the opposite of `file-storing`, which uses Mapperly — deliberately.)

### Go through the managers, not the repository

The read/inbox side doesn't touch a repository directly — it goes through Core's domain-service-level
abstractions (`IUserNotificationManager`, `INotificationSubscriptionManager`, `INotificationDefinitionManager`),
which internally delegate to `INotificationStore`. Prefer these managers over reaching for
`IRepository<T, Guid>` directly, unless the manager genuinely has no suitable method.

### Explicit controllers, not conventional/auto ones

`HttpApi` exposes explicit controllers under `/api/notification-center` (`UserNotificationController` for the
inbox, `NotificationSubscriptionController` for subscriptions, plus `AddApplicationPartIfNotExists`). Don't
assume ABP auto API controller behaviour here.

### Display text is localized at read time

`NotificationDisplayName` is localized **per the current reader's culture, inside `MapToDto`** — not baked in
at publish time. Keep this if you touch that method; the opposite was a real bug in the legacy implementation,
because background-job distribution runs without a request culture.

## Authorization — two layers

1. **Standard ABP permissions** gate `NotificationCenter`'s own AppServices/Controllers — e.g. an admin-only
   "manage all subscriptions" endpoint uses `[Authorize(...)]` exactly like any other ABP module.

2. **`INotificationPermissionChecker`** (in Core, `Dignite.Abp.Notifications`) is a separate, pluggable
   abstraction that gates whether a *given user* is allowed to **receive** a given notification definition —
   checked during distribution (`NotificationDefinitionManager` / `DefaultNotificationDistributor`), not on an
   AppService call. The default is `AlwaysGrantedNotificationPermissionChecker`; `Notifications.Identity`
   supplies a real implementation backed by ABP Identity/Authorization.

When adding a new notification type that should be permission-gated, wire it through
`INotificationDefinitionProvider`/`NotificationDefinition` — don't try to gate it with an AppService-style
`[Authorize]` attribute; there's no controller action being called at that point.

**AppService-level authorization is a bare `[Authorize]`.** Any authenticated user may manage **their own**
inbox/subscriptions — enforced by always scoping to `CurrentUser.GetId()`, not by a fine-grained permission
name. Don't add a permission constant for "read your own inbox"; **do** add one for anything that touches
*other* users' data.

`NotificationDefinition.PermissionName` and `FeatureName` constrain both subscription **and** delivery — never
treat an explicit `userIds` array as a bypass (`notifications-invariants` §7).

## Infrastructure posture

### The distributed event: `NotificationDeliveryRequestedEto`

Wire name `Dignite.Abp.Notifications.NotificationDeliveryRequested`. Core's internal handler adapts transport
to the canonical `INotificationNotifier.DeliverAsync` contract; **channel plugins do not implement distributed
event handlers**. Distributed events are how Core reaches every Notifier.

Before touching it, read `notifications-invariants` §1 (serialization) and §4 (single-recipient and
cancellation guarantees). In particular: ABP serializes ETOs with plain System.Text.Json and *no* app-level
options — the transactional outbox/inbox included — so a polymorphic/abstract member on an ETO is lossy on
write and throws on read. Keep every ETO a flat, default-STJ-round-trippable POCO.

### Features gate notification *definitions*, not just endpoints

Same `PermissionName`/`FeatureName` pair as "Authorization — two layers" above — declared per
`NotificationDefinition` via `INotificationDefinitionProvider`, checked by `INotificationDefinitionManager`
**at distribution time**, not just on the AppService call.

### The background job: `NotificationDistributionJob`

`INotificationPublisher` enqueues it when the explicit recipient count exceeds the (currently hardcoded)
direct-distribution threshold, instead of distributing inline. A large explicit fan-out goes to a **single**
background job carrying the caller's list; the job's distributor batches internally (`RecipientBatchSize`).

**Preserve the notification tenant on every job** (`notifications-invariants` §8). Don't reintroduce a
prepared-notification/eligibility-mode multi-job split; it was removed as over-engineering.

### Logging

**Do not log recipient IDs.** A single summary log line per distribution is enough — don't reintroduce the
per-recipient OpenTelemetry meter or stage instrumentation (`notifications-invariants` §8).

### Localization

```csharp
[LocalizationResourceName("NotificationCenter")]
public class NotificationCenterResource { }
```

See "Display text is localized at read time" above — the same rule applies to this resource.

## Anti-patterns specific to this module

| Don't | Do instead |
|---|---|
| CLR type name / `AssemblyQualifiedName` as a wire discriminator | A stable `[NotificationDataType]` discriminator — `notifications-invariants` §1 |
| Newtonsoft.Json anywhere in the notification pipeline | System.Text.Json only, through `INotificationDataSerializer` |
| A polymorphic/abstract member on an ETO | A flat, default-STJ-round-trippable POCO (`DataJson`) — `notifications-invariants` §1 |
| `typeof(Order)` for `EntityTypeName` | A stable caller-chosen string: `new NotificationEntityIdentifier("Demo.Order", orderId)` |
| A new custom repository interface per aggregate | A new method on `INotificationStore` |
| Mapperly/AutoMapper in the AppService | The hand-written `protected virtual MapToDto(...)` |
| A singleton manager injecting `INotificationStore` | `ITransientDependency` — `notifications-invariants` §2 |
