# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) — with one
deviation from the classic scheme: see [CONTRIBUTING.md → Versioning and releases](CONTRIBUTING.md#versioning-and-releases)
for why `MAJOR` tracks the targeted ABP Framework version rather than this module's own breaking
changes.

## [Unreleased]

## [10.0.0-rc.3] - 2026-07-23

> This pre-stable line explored a much larger feature surface (per-recipient delivery reliability with leases /
> retries / dead-lettering / force-delivery, per-user delivery preferences + quiet hours, large-audience broadcast
> orchestration, payload schema-versioning + upcasters, opt-in definition payload/entity contracts, a replaceable
> batch eligibility evaluator + trusted-recipient bypass API, distribution metrics, a prepared multi-job
> fan-out, and a scheduled retention/lifecycle-cleanup worker + options) and then **cut all of it before release**
> as over-engineering for a best-effort in-app notification module. None of it shipped, so it is not documented as removed below. The module's positioning is deliberately
> "best-effort in-app notifications": delivery is fire-once (the inbox row is authoritative), and distributed-systems
> machinery (delivery reliability/retries, broadcast jobs, schema-evolution upcasters) is intentionally absent.

### Added

- MongoDB integration with ABP's distributed event outbox and inbox through `UseNotificationCenterMongoDbOutbox()`,
  using ABP-compatible event-box collections with query indexes and sharing atomic commit/rollback tests with EF
  Core. The transactional outbox requires a transaction-capable MongoDB 4.0+ replica set and transactional ABP
  units of work.
- Scoped subscription application/REST contracts that round-trip the stable entity type and ID, while retaining the
  name-only methods as definition-wide compatibility wrappers. MVC and Angular subscription UIs submit the complete
  scope.
- Tolerant notification-data reads: `INotificationDataSerializer.Deserialize(json)` returns a safe
  `UnsupportedNotificationData` placeholder for unknown or malformed payloads instead of throwing, so one bad
  historical row cannot fail a whole inbox page. MVC and Angular render it with a localized fallback. (A
  strict-vs-tolerant read-mode switch existed briefly pre-release; it was cut because every real read boundary
  already chose tolerant, so strict mode had no caller. Not documented as removed below, per the note above.)
- A bounded recipient pipeline: `INotificationStore.GetSubscriptionUserIdsAsync` keyset paging plus bounded inbox
  multi-insert. Explicit fan-outs above `NotificationDistributionOptions.DirectDistributionUserThreshold` run on a
  single background job whose distributor batches recipients internally (`RecipientBatchSize`).

### Changed

- **Breaking NotificationCenter package-family rename before 10.0.0 stable.** The optional Notification Center
  packages, namespaces, and module class names now use `Dignite.NotificationCenter*` instead of
  `Dignite.Abp.NotificationCenter*`. This is a naming-only change with no functional behavior change.
- **Breaking application/domain API alignment before 10.0.0 stable.** Current-user inbox services are now
  `IUserNotificationAppService` / `UserNotificationAppService`, and `GetCountAsync` is `GetNotificationCountAsync`.
  Pass-through manager interfaces and `UserNotificationManager` were removed; application reads now use
  `INotificationStore` while the concrete `NotificationSubscriptionManager` owns validated subscription mutation.
  REST routes are unchanged.
- **Breaking options split before 10.0.0 stable.** The catch-all `NotificationOptions` type was replaced by
  `NotificationDefinitionRegistration` (provider registration) and `NotificationDistributionOptions` (inline/background
  threshold + `RecipientBatchSize`, capped by `MaxBatchSize` = 10,000, validated on startup). Custom constructors
  and `IOptions<T>` consumers must adopt the responsible option type and be recompiled; no database migration.
- **Breaking notifier contract.** `INotificationNotifier` is the sole channel execution contract: `Name` plus
  cancellation-aware single-recipient `DeliverAsync`. Delivery is best-effort — `DeliverAsync` returns `Task`
  (no result type); a notifier skips a recipient by returning, and a throw is logged and dropped by the Core
  handler, not retried. The Core-owned distributed-event handler adapts transport, so channel plugins do not
  implement an event-handler interface.
- **Breaking distributed-event contract.** The default distributor publishes single-recipient/channel
  `NotificationDeliveryRequestedEto` (wire name `Dignite.Abp.Notifications.NotificationDeliveryRequested`) instead of
  the legacy batched aggregate event. Quiesce publication, drain old aggregate events, upgrade consumers, then producers.
- **Breaking distributed-event payload envelope.** `NotificationDeliveryRequestedEto.Data` (a live, abstract
  `NotificationData`) is replaced by `DataJson`: the payload pre-serialized as discriminator-tagged JSON via
  `INotificationDataSerializer` at the distributor publish boundary and hydrated at the notifier boundary
  (`NotificationPayload.FromRequest(request, dataSerializer)`; both built-in notifiers inject the serializer). ABP's
  event bus — including the transactional outbox/inbox — serializes ETOs with plain System.Text.Json and no
  app-level options, so an abstract member cannot round-trip the box; a pre-serialized string survives any transport
  and stays readable for non-.NET consumers. Recompile channel plugins and rebuild custom notifiers against the new
  member. The rename is deliberate: pre-upgrade outbox rows (whose `Data` was written lossily) drain as a null
  payload under the new contract instead of poisoning the sender with a type mismatch.
- **Breaking pre-stable naming cleanup.** The single-recipient payload type `NotificationDelivery` (added in
  10.0.0-preview.2) is renamed to `NotificationPayload`, and its factory `FromWorkItem` to `FromRequest`, so the
  "delivery" vocabulary names only the act while the payload type reads as content. The delivery event's wire name is
  normalized from `Dignite.Abp.Notifications.NotificationDeliveryWork` to
  `Dignite.Abp.Notifications.NotificationDeliveryRequested` to match its `NotificationDeliveryRequestedEto` contract
  name. Recompile channel plugins/remote consumers and drain any in-flight events on the old wire name before upgrading.
- **Breaking for MongoDB context implementers.** `INotificationCenterMongoDbContext` extends ABP's `IHasEventInbox`
  and `IHasEventOutbox`. Consumer-owned implementations must expose and model the two ABP event collections and
  configure both boxes against their custom context. Notification business records require no backfill or rename.
- **Breaking behavior for callers.** An explicit `userIds` array no longer bypasses a notification definition's
  permission and feature requirements: `PublishAsync` filters explicit and subscription-derived recipients through
  the same `INotificationDefinitionManager.IsAvailableAsync` check, in the notification's tenant or host context.
- **Breaking behavior for direct distributor callers.** `NotificationInfo.TenantId` is authoritative for
  subscription lookup, eligibility, persistence, and event/outbox publication. `null` always means host and never
  falls back to an ambient tenant, so tenant-side callers of `INotificationDistributor` must set it explicitly.
- Subscription-driven distribution treats a definition-wide subscription as a fallback for every entity and combines
  it with an exact entity subscription without delivering twice to the same user. Subscription uniqueness uses
  non-null, ordinal identity keys across EF Core and MongoDB; existing databases need a host-owned backfill and
  index migration as documented in the README.

### Fixed

- Hosts routing the distributed event bus through ABP's transactional outbox (`UseNotificationCenterEfCoreOutbox()`
  / `UseNotificationCenterMongoDbOutbox()`) could never deliver to SignalR or email: draining
  `NotificationDeliveryRequestedEto` from the box threw `System.NotSupportedException` on the abstract `Data`
  member (ABP deserializes outbox/inbox payloads with plain System.Text.Json), and the write side had already
  dropped the derived payload fields. Fixed by the `DataJson` envelope above; the shared outbox contract tests now
  drain both event boxes and assert the concrete payload survives. ([#118](https://github.com/dignite-projects/abp-notifications/issues/118))
- Notification definition names and `NotificationData` discriminators use explicit ordinal, case-sensitive
  registration and lookup. Conflicting registrations fail during application startup with both providers or CLR
  mappings identified instead of silently replacing an earlier value. Definition providers are discovered across
  module assemblies and duplicate registrations of the same provider execute once.
- Consistent explicit-recipient semantics across inline and background distribution: `null` resolves subscriptions,
  an empty list is a no-op, and duplicate explicit user IDs are normalized before threshold selection, persistence,
  and channel delivery.
- `NotificationDistributionJob` had two public `ExecuteAsync` overloads, so ABP's `BackgroundJobExecuter` (which
  resolves a job's execute method by name via reflection) threw `AmbiguousMatchException` for every
  background-dispatched distribution — any publish without explicit `userIds` (subscription-driven notifications,
  or a large explicit fan-out) silently failed while the triggering AppService call still returned 200/204. The
  second overload is renamed to `ExecuteWithCancellationAsync`. Fixing that exposed the job never opening a Unit of
  Work, so `NotificationStore.GetSubscriptionUserIdsAsync` threw `ObjectDisposedException` on the `DbContext`; the
  job now wraps distribution in `UnitOfWorkManager.Begin(requiresNew: true)` so the notification insert, inbox
  rows, and outbox event write commit atomically.

## [10.0.0-rc.2] - 2026-07-16

### Changed

- Renamed the Angular package to `@dignite/ng.notification-center`, making the UI framework
  explicit and leaving room for parallel React or other client packages.
- Clarified that npm requires every package to have a `latest` dist-tag, so a package whose first
  public version is a pre-release temporarily exposes that version as both `next` and `latest`.

## [10.0.0-rc.1] - 2026-07-16

### Added

- Added CI coverage for the Angular library and production demo build.
- Added isolated consumption smoke tests for all 15 packed NuGet packages and both Angular package
  entry points.

### Changed

- Synchronized the Angular package version with the NuGet release version.
- Renamed the Angular package to `@dignite/abp-notification-center` so all Dignite npm packages
  share the `@dignite` organization scope.
- Tagged pre-releases are now published publicly to NuGet.org and npm in addition to the existing
  GitHub Packages preview feed.
- Replaced the long-lived NuGet API key with NuGet.org Trusted Publishing and a short-lived OIDC
  credential issued to the tagged release workflow.
- Expanded the README with package installation commands, a compatibility table, and migration
  guidance for legacy 3.x consumers.

## [10.0.0-preview.2] - 2026-07-10

> `MAJOR` tracks the targeted ABP Framework version, so a breaking change to this module's own
> contracts arrives in a `MINOR` bump. Entries below marked **Breaking** require action when
> upgrading.

### Added

- Added `Dignite.Abp.Notifications.Emailing.Identity`, an optional ABP Identity-backed
  `IEmailNotificationAddressResolver` for the Emailing notifier.
- `NotificationDeliveryRequestedEto` and `NotificationDelivery` now carry the notification's `EntityTypeName` and
  `EntityId`, so a notifier can identify the business entity a notification is about without depending on
  Core.
- Added `NotificationEmailContentProvider<TData>`, a base class that narrows `NotificationData` once so an
  implementer cannot forget the type guard and accidentally claim every notification.
- Email address resolvers can now return an optional recipient culture; email content is built inside that culture
  and falls back to `NotificationEmailOptions.DefaultCulture`.

### Changed

- Changed email address resolution to use `EmailNotificationAddressResolveContext`, making
  `TenantId` explicit to local and remote resolver implementations.
- **Breaking.** `NotificationEntityIdentifier` now takes `(string entityTypeName, string entityId)` instead of
  `(Type entityType, object entityId)` — pass a short, stable name such as `"Demo.Order"`. Persisted
  `EntityTypeName` values are therefore no longer `Type.FullName`, so subscription rows and
  `NotificationCenterWebOptions.EntityLinkResolvers` keys written in the old format stop matching.
- **Breaking.** `IEmailNotificationAddressResolver` gained an `Order` member, and `GetEmailOrNullAsync` returns
  `Task<EmailNotificationAddress?>` rather than `Task<string?>`. Resolvers now form an ordered chain in which the
  first non-null address wins.
- **Breaking.** `IdentityEmailNotificationAddressResolver` no longer declares `[Dependency(ReplaceServices = true)]`.
  It joins the chain at `NotificationEmailProviderOrders.BuiltInFallback`, so an application resolver composes with
  it rather than displacing it.
- **Breaking.** Renamed `NotificationEmailContentProviderOrders` to `NotificationEmailProviderOrders`, which now
  orders both the content-provider and the address-resolver chains.
- **Breaking.** `NotificationEmailBuildContext`'s constructor gained a `cultureName` parameter.
- Documented that atomic persist-and-publish, and deduplication of a redelivered event, require the host to enable
  ABP's transactional outbox — `UseNotificationCenterEfCoreOutbox()` on EF Core. The MongoDB provider wires neither.
  No behaviour changed; the previous comments and README described a guarantee that was conditional.

### Fixed

- `EmailNotifier` no longer aborts the whole delivery when a recipient's culture name cannot be parsed. It falls
  back to `NotificationEmailOptions.DefaultCulture` and then to the ambient culture, logging a warning for each
  rejected value. Previously a single malformed per-user language setting threw out of the event handler, so every
  recipient after it received nothing and a redelivery re-mailed the ones before it. As part of this,
  `NotificationEmailBuildContext.CultureName` now accepts the empty string, which is the invariant culture.

### Removed

- **Breaking.** Removed `NullEmailNotificationAddressResolver` — an empty resolver chain already resolves no
  address.

## [10.0.0-preview.1] - 2026-07-09

### Added

- Initial public release: an event-driven, pluggable notification framework for ABP Framework
  (`core/`), plus an optional Notification Center providing a persistent inbox, subscriptions,
  read/unread state, and a REST API, with MVC and Angular UI libraries
  (`notification-center/`).
- Dual persistence support (EF Core and MongoDB) behind a shared `INotificationStore` abstraction.
- Real-time push (SignalR) and email notifiers.
- Optional permission gating for notification definitions via ABP Identity.
