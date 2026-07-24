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

## Settings

### Define Settings
```csharp
public class FileExplorerSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        context.Add(new SettingDefinition(FileExplorerSettings.GroupName + ".SomeSetting", "defaultValue"));
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
        var value = await _settingProvider.GetOrNullAsync(FileExplorerSettings.GroupName + ".SomeSetting");
    }
}
```

> **In this repo**: `FileExplorerSettings` (Domain) + `FileExplorerSettingDefinitionProvider` are present but
> mostly a **placeholder** (the group name is defined; there are no shipped settings yet). Add real settings
> here rather than inventing a new provider. Per-container upload limits are **not** settings — they're
> `BlobContainerConfiguration` (see `template/app.md`).

## Features

```csharp
public class MyFeatureDefinitionProvider : FeatureDefinitionProvider
{
    public override void Define(IFeatureDefinitionContext context)
    {
        var group = context.AddGroup("FileExplorer");
        group.AddFeature("FileExplorer.SomeFeature", defaultValue: "false", valueType: new ToggleStringValueType());
    }
}

[RequiresFeature("FileExplorer.SomeFeature")]
public async Task DoAsync() { /* ... */ }
```

This repo does not currently define features; the mechanism is available if a per-tenant toggle is genuinely
needed.

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
```

> **Caching tip for image ops**: the on-the-fly image-resize endpoint has no cache today, so repeated resize
> requests re-decode and re-encode. If you add caching there, key it on the resolved (bounded) dimensions plus
> the source blob identity — not on raw client-supplied width/height.

## Event Bus

### Local Events (Same Process)
```csharp
public class SomethingCreatedEventHandler : ILocalEventHandler<SomethingCreatedEvent>, ITransientDependency
{
    public async Task HandleEventAsync(SomethingCreatedEvent eventData) { /* same transaction */ }
}
```

### Distributed Events (Cross-Service)
```csharp
[EventName("MyApp.Something.Created")]
public class SomethingCreatedEto { public Guid Id { get; set; } }
```

> **In this repo**: the modules **do not publish local or distributed events**, and the DbContext has **no**
> transactional outbox/inbox (no `IHasEventInbox`/`IHasEventOutbox`). The upload pipeline (`IFileHandler`)
> and the file/directory managers run **inline** inside the request's unit of work. Don't add an ETO/outbox
> to make "store metadata" + "write blob" atomic — that consistency problem is handled with ordering and
> compensation at the manager level (see `file-storing-invariants.md`), not with distributed events.

## Background Jobs

```csharp
public class SomeJob : AsyncBackgroundJob<SomeArgs>, ITransientDependency
{
    public override async Task ExecuteAsync(SomeArgs args) { /* ... */ }
}
```

> **In this repo**: there are no background jobs in the module packages. If you introduce one (e.g. orphan-blob
> cleanup), carry the tenant on the args and re-establish it in the handler — don't inherit ambient tenant
> state from whoever enqueued it.

## Localization

```csharp
[LocalizationResourceName("FileExplorer")]
public class FileExplorerResource { }
```

- In `ApplicationService`/`AbpController`: use the `L["Key"]` property.
- In other services: inject `IStringLocalizer<FileExplorerResource>`.
- **Keep error-code namespaces and resource keys aligned.** `FileExplorerErrorCodes` values, the
  `en.json`/`*.json` resource keys, and the `MapCodeNamespace(...)` mapping must agree — a mismatch there
  meant business exceptions didn't localize (fixed in "align file explorer error localization").

> **Tip**: ABP base classes already provide commonly used services as properties. Check before injecting:
> `L`, `Clock`, `CurrentUser`, `CurrentTenant`, `GuidGenerator`, `AuthorizationService`, `FeatureChecker`,
> `DataFilter`, `LoggerFactory`, `Logger`. Plain classes (an `IFileHandler`, `RandomBlobNameGenerator`) don't
> get these for free — see `framework/common/abp-core.md`.
