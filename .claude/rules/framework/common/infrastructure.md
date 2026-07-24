---
paths:
  - "**/*Setting*.cs"
  - "**/*Feature*.cs"
  - "**/*Cache*.cs"
  - "**/*Event*.cs"
  - "**/*Job*.cs"
---

# ABP Infrastructure Services

> **Docs**: https://abp.io/docs/latest/framework/infrastructure
>
> Generic ABP conventions first, then a per-module section. **What each module deliberately doesn't
> have is the load-bearing part** — read your module's section before adding an event, job, or cache.

## Settings

### Define Settings
```csharp
public class MySettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(new SettingDefinition("MyApp.MaxItemCount", "10"));
    }
}
```

### Read Settings
```csharp
public class MyService : ITransientDependency
{
    private readonly ISettingProvider _settingProvider;

    public async Task DoSomethingAsync()
    {
        var maxCount = await _settingProvider.GetAsync<int>("MyApp.MaxItemCount");
    }
}
```

## Features

### Define Features
```csharp
public class MyFeatureDefinitionProvider : FeatureDefinitionProvider
{
    public override void Define(IFeatureDefinitionContext context)
    {
        var group = context.AddGroup("MyApp");
        group.AddFeature("MyApp.PdfReporting", defaultValue: "false", valueType: new ToggleStringValueType());
    }
}
```

### Check Features
```csharp
[RequiresFeature("MyApp.PdfReporting")]
public async Task<PdfReportDto> GetPdfReportAsync() { /* ... */ }

if (await _featureChecker.IsEnabledAsync("MyApp.PdfReporting")) { /* ... */ }
```

## Distributed Caching

```csharp
public class MyService : ITransientDependency
{
    private readonly IDistributedCache<MyCacheItem> _cache;

    public async Task<MyCacheItem> GetAsync(Guid id)
    {
        return await _cache.GetOrAddAsync(
            id.ToString(),
            async () => await LoadFromDatabaseAsync(id),
            () => new DistributedCacheEntryOptions { AbsoluteExpiration = Clock.Now.AddHours(1) });
    }
}

[CacheName("MyItems")]
public class MyCacheItem { public string Name { get; set; } }
```

## Event Bus

### Local Events (Same Process)
```csharp
public class SomethingCreatedEventHandler : ILocalEventHandler<SomethingCreatedEvent>, ITransientDependency
{
    public async Task HandleEventAsync(SomethingCreatedEvent eventData) { /* same transaction */ }
}

await _localEventBus.PublishAsync(new SomethingCreatedEvent { ... });
```

### Distributed Events (Cross-Service)
```csharp
[EventName("MyApp.Something.Created")]
public class SomethingCreatedEto
{
    public Guid Id { get; set; }
}

public class SomethingCreatedEtoHandler : IDistributedEventHandler<SomethingCreatedEto>, ITransientDependency
{
    public async Task HandleEventAsync(SomethingCreatedEto eventData) { /* ... */ }
}
```

### When to Use Which
- **Local**: within the same module / bounded context
- **Distributed**: cross-module or cross-process communication

> ⚠️ **The two modules take opposite positions on distributed events, and both are deliberate.**
> `file-storing` publishes **none**; `notifications` is built around exactly one.

## Background Jobs

```csharp
public class SomeJob : AsyncBackgroundJob<SomeArgs>, ITransientDependency
{
    public override async Task ExecuteAsync(SomeArgs args) { /* ... */ }
}

await _backgroundJobManager.EnqueueAsync(new SomeArgs { ... }, delay: TimeSpan.FromMinutes(5));
```

**Whatever the module, carry the tenant on the args and re-establish it in the handler** — never inherit
ambient tenant state from whoever enqueued the job.

## Localization

```csharp
[LocalizationResourceName("MyModule")]
public class MyModuleResource { }
```

- In `ApplicationService`/`AbpController`: use the `L["Key"]` property
- In other services: inject `IStringLocalizer<MyModuleResource>`

> **Tip**: ABP base classes already provide commonly used services as properties. Check before injecting: `L`,
> `Clock`, `CurrentUser`, `CurrentTenant`, `GuidGenerator`, `AuthorizationService`, `FeatureChecker`,
> `DataFilter`, `LoggerFactory`, `Logger`. Plain classes (not inheriting an ABP base class) don't get these for
> free — see `abp-core.md`.

---

## In `file-storing`

### Settings — a placeholder, not a config surface
`FileExplorerSettings` (Domain) + `FileExplorerSettingDefinitionProvider` exist but are mostly a
**placeholder** (the group name is defined; there are no shipped settings yet). Add real settings there rather
than inventing a new provider.

**Per-container upload limits are NOT settings** — they're `BlobContainerConfiguration` (see
`file-storing/.claude/rules/template/app.md`).

### Features — none defined
The mechanism is available if a per-tenant toggle is genuinely needed.

### Caching — the image-resize gap
The on-the-fly image-resize endpoint has **no cache today**, so repeated resize requests re-decode and
re-encode. If you add caching there, key it on the resolved (bounded) dimensions plus the source blob identity
— **not** on raw client-supplied width/height. See `file-storing-invariants.md` §1 for why bounds come first.

### Event bus — this module publishes nothing
No local or distributed events, and the DbContext has **no** transactional outbox/inbox (no
`IHasEventInbox`/`IHasEventOutbox`). The upload pipeline (`IFileHandler`) and the file/directory managers run
**inline** inside the request's unit of work.

**Don't add an ETO/outbox to make "store metadata" + "write blob" atomic** — that consistency problem is
handled with ordering and compensation at the manager level (`file-storing-invariants.md` §4). This is on the
module's explicit anti-scope list (§10).

### Background jobs — none in the module packages
If you introduce one (e.g. the orphan-blob cleanup sweep the audit recommends), carry the tenant on the args
and re-establish it in the handler.

### Localization
```csharp
[LocalizationResourceName("FileExplorer")]
public class FileExplorerResource { }
```
**Keep error-code namespaces and resource keys aligned.** `FileExplorerErrorCodes` values, the `*.json`
resource keys, and the `MapCodeNamespace(...)` mapping must agree — a mismatch meant business exceptions
didn't localize (fixed in "align file explorer error localization").

---

## In `notifications`

### Features gate notification *definitions*, not just endpoints
A `NotificationDefinition` (registered through `INotificationDefinitionProvider`) can declare Feature and
Permission requirements, checked by `INotificationDefinitionManager` **at distribution time** — see
`framework/common/authorization.md`, and `notifications-invariants.md` §7 for why an explicit `userIds` list is
not a bypass.

### The distributed event: `NotificationDeliveryRequestedEto`
Wire name `Dignite.Abp.Notifications.NotificationDeliveryRequested`. Core's internal handler adapts transport to
the canonical `INotificationNotifier.DeliverAsync` contract; **channel plugins do not implement distributed
event handlers**.

Before touching it, read `notifications-invariants.md` §1 (serialization) and §4 (single-recipient and
cancellation guarantees). In particular: ABP serializes ETOs with plain System.Text.Json and *no* app-level
options — the transactional outbox/inbox included — so a polymorphic/abstract member on an ETO is lossy on
write and throws on read. Keep every ETO a flat, default-STJ-round-trippable POCO.

Distributed events are also how Core reaches every Notifier.

### The background job: `NotificationDistributionJob`
`INotificationPublisher` enqueues it when the explicit recipient count exceeds the (currently hardcoded)
direct-distribution threshold, instead of distributing inline. A large explicit fan-out goes to a **single**
background job carrying the caller's list; the job's distributor batches internally (`RecipientBatchSize`).

**Preserve the notification tenant on every job** — see `notifications-invariants.md` §8. Don't reintroduce a
prepared-notification/eligibility-mode multi-job split; it was removed as over-engineering.

### Logging
**Do not log recipient IDs.** A single summary log line per distribution is enough — don't reintroduce the
per-recipient OpenTelemetry meter or stage instrumentation (`notifications-invariants.md` §8).

### Localization
```csharp
[LocalizationResourceName("NotificationCenter")]
public class NotificationCenterResource { }
```
This module localizes a notification's display text **at read time** (per the reader's culture), not at
publish/distribution time — see `framework/common/application-layer.md`.
