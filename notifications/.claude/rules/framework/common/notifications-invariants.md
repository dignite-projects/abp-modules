# Hard Invariants — Read Before Touching Notifications / Notifiers / DI Lifetimes

> This file has **no `paths:` frontmatter, so it always loads**, alongside `abp-core.md` and
> `template/app.md`. It exists because this repo is a from-scratch rewrite of a legacy
> implementation that shipped with the exact bugs described below —
> these are not style preferences, they're the reasons this repo exists. Do not reintroduce them.

## The subtraction that shaped these rules

Many rules below read as prohibitions ("removed", "don't reintroduce X") because X was actually built
and then taken back out. After the from-scratch rewrite, an **agent-driven issue spree (#54–#88)** grew
this module toward a *distributed delivery platform*: per-recipient/per-channel delivery state machines
with leases, idempotency keys, and retries; large-audience broadcast orchestration with progress
persistence; schema-versioned payloads with upcaster chains; a replaceable recipient-eligibility policy
engine plus a trusted-system bypass; definition→payload/entity type contracts; retention cleanup;
per-recipient delivery telemetry. Four subtraction rounds (**PRs #99–#103**) removed roughly **17k net
lines** of it.

The error was not "all wrong" — it was **the right fixes wrapped in the wrong-scale infrastructure**.
Roughly a third of #54–#88 was genuine and was kept, several as the hard invariants below: the stable
`[NotificationDataType]` discriminator + tolerant reads (§1), entity-scoped subscription identity (§6),
delivery-time permission/feature gating (§7), bounded batching + keyset paging (§8), the MongoDB outbox,
notifier cancellation (§3/§4), and ABP-convention alignment. The subtraction was surgical: strip the
distributed-systems shell, keep the correctness fixes inside it.

The north star is this module's identity — **best-effort, in-app notification**: a durable inbox plus
fire-once channel delivery, not an at-least-once delivery platform. If a proposed feature only earns its
keep for a delivery platform (reliability guarantees, replayable schema evolution, pluggable policy,
broadcast scale, delivery metrics), it does not belong here. Test each addition against that identity —
not against "what a mature notification system has." The later user-facing API pass (controller split,
merged subscription endpoints, `GetUnreadCountAsync`/`DeleteAllReadAsync`) applied the same instinct at
the surface level: narrow to intent, don't preserve breadth for its own sake.

## 1. `NotificationData` serialization: stable discriminator, System.Text.Json only

**Never** use `data.GetType().AssemblyQualifiedName`/`.FullName` or `Type.GetType(...)` as the
wire/storage type identifier for a `NotificationData` subclass, and **never** mix in
Newtonsoft.Json anywhere in this pipeline.

- Every `NotificationData` subclass must carry a short, stable discriminator via
  `[NotificationDataType("...")]` (e.g. `"Dignite.Message"`) — a name that survives assembly
  version bumps and works for remote/non-.NET consumers reading the same JSON.
- Discriminators use ordinal, case-sensitive comparison and must form a one-to-one mapping with CLR
  payload types. Registering one key for two types, or one type under two keys, fails during startup;
  repeating the exact same key/type pair is the only supported idempotent repeat.
- All serialization goes through the shared `INotificationDataTypeRegistry` /
  `NotificationDataJsonConverter` / `INotificationDataSerializer` — never a one-off converter or a
  hand-rolled switch statement in a specific layer. Storage (EF Core / MongoDB) and the HTTP API use
  the converter registered once, globally, on `AbpSystemTextJsonSerializerOptions` in
  `AbpNotificationsAbstractionsModule.ConfigureServices` (so remote `HttpApi.Client` consumers get it too).
- **The distributed event bus never sees a live `NotificationData`.** ABP serializes ETOs with plain
  `System.Text.Json` and *no* app-level options — the transactional outbox/inbox included — so a
  polymorphic/abstract member on an ETO is lossy on write and throws on read
  (`NotSupportedException` while draining the box; issue #118). `NotificationDeliveryRequestedEto`
  therefore carries the payload pre-serialized as `DataJson`, produced via
  `INotificationDataSerializer.Serialize` at the distributor publish boundary and hydrated via
  `INotificationDataSerializer.Deserialize` (`NotificationPayload.FromRequest(request, dataSerializer)`)
  at the notifier boundary. Keep every ETO a flat, default-STJ-round-trippable POCO; never put an
  abstract/polymorphic member back on one.
- The payload envelope carries only the stable `type` discriminator — no `schemaVersion`, no upcaster chain.
  (An earlier design added event-sourcing-style schema versioning + N→N+1 upcasters; it was removed as
  over-engineering — notifications are read-once, not a replayable event stream. Don't reintroduce it.)
- Every read is tolerant: unknown discriminators and malformed known data become `UnsupportedNotificationData`
  instead of throwing. A newer payload of a *known* type reads leniently (extra members land in
  `ExtensionData`). Preserve raw JSON only as escaped diagnostic data; never interpret it as a CLR name or show
  it in the fallback UI.
- Use the canonical `INotificationDataSerializer.Deserialize(json)`. Do not reintroduce optional reader
  capability probes, a schema-evolution/upcaster registry, or a strict-vs-tolerant read-mode switch — an
  earlier design had one (`NotificationDataReadMode.Strict` + `NotificationDataReadException`), but every real
  read boundary (durable inbox, distributed-event, HTTP) already chose tolerant, so strict mode was unreachable
  dead API surface. Writing an unregistered CLR type still throws `JsonException` — that fail-fast stays.
- **Why**: the legacy implementation wrote with System.Text.Json + `AssemblyQualifiedName`, read
  back with Newtonsoft + `Type.GetType()`, and had a separate hardcoded switch (only 2 types) in
  the HTTP client converter. Result: an assembly version bump could make historical notifications
  undeserializable, and custom `NotificationData` subclasses silently broke for any remote .NET
  client. This was priority **P0** — the "load-bearing wall" bug, because every Notifier and every
  remote consumer sits downstream of it.
- Add a round-trip test for any new `NotificationData` subclass: publish → persist → deserialize on
  a "remote" client, and assert the JSON contains your discriminator, not a CLR type name.

**The same rule governs `EntityTypeName`.** `NotificationEntityIdentifier` takes a stable,
caller-chosen string — `new NotificationEntityIdentifier("Demo.Order", orderId)`, never
`typeof(Order)`. That value is persisted on `Notification` and `NotificationSubscription`, matched by
plain string equality when `NotificationStore` resolves subscribers, returned over REST as
`UserNotificationDto.EntityTypeName`, and used as the key of
`NotificationCenterWebOptions.EntityLinkResolvers`. A `Type.FullName` there silently orphans every
stored subscription and breaks every bell link the day someone renames a namespace — the same failure
mode as above, on a different field.

## 2. DI lifetime discipline — never let a Singleton capture a Scoped dependency

Before marking a service `ISingletonDependency`, check every constructor dependency (transitively)
for anything backed by a repository, `DbContext`, or other per-request/per-unit-of-work state
(`INotificationStore` in Center mode is exactly this). If it's request-scoped, the service must be
`ITransientDependency` (or resolve the scoped dependency from `IServiceProvider` on demand, not via
constructor injection).

- **Why**: the legacy `UserNotificationManager` was `ISingletonDependency` while injecting
  `INotificationStore` (which, with Center installed, holds repositories/DbContext) — a
  copy-paste/typo bug from its sibling `NotificationSubscriptionManager` (correctly transient).
  Autofac doesn't validate this by default, so it doesn't fail at startup; it fails under
  concurrent load with thread-unsafe `DbContext` use or a UoW that doesn't match the current
  request. Definition/registry caches (name → definition lookups) are fine as singletons — the
  permission/store checks that ride along with them are not.

## 3. Notifiers are plugins — depend on `Abstractions`, not Core or Center

A Notifier (SignalR, Email, future WebPush/FCM/SMS/Webhook) references
`Dignite.Abp.Notifications.Abstractions` and its own channel SDK — nothing else in this repo. It
implements `INotificationNotifier` and handles one `NotificationDeliveryRequestedEto` through cancellation-aware
`DeliverAsync`; Core's internal handler owns distributed transport adaptation. A Notifier should not need
`INotificationStore`, EF Core, or MongoDB. This is what lets a channel be added,
removed, or deployed independently without touching Core.

There is **no exception left** — `Emailing` used to be one and no longer is; its `.csproj` has a
single `ProjectReference`, to `Abstractions`. Don't reintroduce one. Host-specific address or
identity lookups belong in a separate integration package, as `Notifications.Emailing.Identity` does
for ABP Identity. Everything a Notifier needs about the notification — including the entity it
concerns (`EntityTypeName` / `EntityId`) — rides on the ETO.

## 4. Delivery is best-effort, single-recipient, and cancellation-aware

`NotificationDeliveryRequestedEto` carries exactly one `UserId` and one channel. Never reintroduce an aggregate
recipient list at this boundary. Delivery is **best-effort**: Core's internal handler resolves the channel notifier
and calls `DeliverAsync` once — there is no per-recipient delivery record, idempotency key, lease, or retry worker.
The authoritative record of a notification is the inbox row; a channel that throws is logged and dropped, not
retried. Forward the supplied `CancellationToken` to channel SDK calls and other cancellable I/O.

(An earlier design added a durable per-recipient/per-channel delivery state machine with leases, idempotency keys,
retries, and a force-delivery override — removed as over-engineering for an in-app notification module. Don't
reintroduce it; use ABP's own distributed-event outbox if at-least-once transport is genuinely required.)

## 5. Both operation modes must keep working

Core logic (publish, distribute, definitions) must function with `NullNotificationStore` (no
Center installed) as well as a real store. Don't add a feature to Core that silently assumes
`NotificationCenter` is present — see "Two operation modes" in `template/app.md`.

## 6. Subscription scope is part of the identity

A subscription is identified by tenant, user, notification definition name, and its optional entity
scope. `EntityTypeName` and `EntityId` are an all-or-nothing pair: both null means every entity for the
definition; both present means one exact entity. Never flatten stored subscriptions to the definition
name in application contracts or UI state.

- Exact checks and deletes operate on the complete identity. Removing one entity scope must not remove
  the definition-wide row or a different entity scope.
- A notification without an entity matches only definition-wide rows. A notification with an entity
  matches definition-wide rows plus its exact entity row, and the distributor deduplicates users in
  that union.
- `SubscribeToAllAvailableNotificationsAsync` creates definition-wide rows only. An existing exact
  entity row does not make the definition-wide row redundant.
- Persistence uniqueness includes the tenant and complete scope. Nullable database uniqueness differs
  across providers, so use the normalized non-null identity keys mapped by both EF Core and MongoDB.

## 7. Definition requirements apply again at delivery

`NotificationDefinition.PermissionName` and `FeatureName` constrain both subscription and delivery.
Never treat an explicit `userIds` array as an implicit authorization or eligibility bypass. Explicit and
subscription-derived candidates run through the same `INotificationDefinitionManager.IsAvailableAsync`
filter in the distributor, after caller-supplied exclusions and before inbox persistence or channel publication.

- Evaluate in the notification's recorded `TenantId`; do not inherit an unrelated ambient tenant from an
  inline caller or background worker. Host (`null`) and tenant contexts must never mix. Direct
  `INotificationDistributor` callers must populate tenant notifications explicitly; `null` is authoritative host,
  not an instruction to fall back to ambient state.
- A failed requirement filters that candidate before any `UserNotificationInfo` or `NotificationDeliveryRequestedEto`
  is produced. Publishing authorization is a separate application-layer concern.

> An earlier design added a replaceable batch `INotificationRecipientEligibilityEvaluator`, an
> enforce/bypass eligibility-mode enum threaded through publisher → job → distributor, and a
> `PublishToExplicitRecipientsWithoutEligibilityChecksAsync` trusted-system bypass. All removed as
> over-engineering: this is best-effort in-app notification, not a pluggable policy engine. Gating is
> simply "don't set `PermissionName`/`FeatureName` if you don't want it." Don't reintroduce a
> replaceable evaluator contract or a requirement-bypassing publish API.

> **Removed — definition payload/entity contracts.** An earlier design let a definition bind itself to a
> stable payload discriminator (`WithPayload<TData>()`) and to a forbidden/optional/required entity identity
> (`WithEntityContract(...)`), validated at startup and again at publish/distribution. Removed as
> over-engineering — it re-implemented a type system to catch a developer publishing the wrong payload, an
> error tests already catch, while the tolerant reader (§1) already renders anything downstream. The stable
> `[NotificationDataType]` discriminator and its registration-conflict fail-fast are what remain. Don't
> reintroduce `WithPayload`/`WithEntityContract` or a `NotificationEntityRequirement` state.

## 8. Recipient work stays bounded

Inline/background selection is not a scalability boundary. Explicit and subscription-derived recipients join
the same bounded candidate → eligibility → persistence → delivery pipeline after resolution.

- Built-in stores page a stable, database-side distinct user-ID query with an exclusive keyset cursor and
  multi-insert only an already-bounded inbox group. Never use offset paging for a mutable subscription set.
  Matching definition-wide and exact entity subscriptions must not create duplicate inbox rows.
- The distributor processes recipients in bounded batches (`RecipientBatchSize`). A large explicit fan-out goes
  to a single background job carrying the caller's list; the job's distributor batches internally. Preserve the
  notification tenant on every job.
- Publish one `NotificationDeliveryRequestedEto` for each recipient/channel after bounded candidate processing.
  Broker limits also include the notification payload.
- Observe cancellation between candidate, store, and event batches. Do not describe cancellation as rollback:
  EF/outbox, EF without outbox, MongoDB, and null-store forwarding have different partial-progress semantics.
- Do not log recipient IDs. Preserve tenant scope on every query, write, job, and event.
- Stable keyset paging and bounded multi-insert belong to canonical `INotificationStore`; cancellation belongs to
  canonical `INotificationDistributor`. `NullNotificationStore` must implement the complete contract without
  persistence.

> An earlier design added per-recipient delivery metrics (an OpenTelemetry meter + candidate/eligible/filtered/
> batch counters + stage instrumentation), a prepared-notification/eligibility-mode multi-job split, and an EF
> change-tracker detach abstraction (`INotificationBatchPersistence`). All removed as over-engineering for an
> in-app module. A single summary log line per distribution is enough; don't reintroduce the meter, the
> multi-job prepared split, or a per-provider batch-persistence seam.
