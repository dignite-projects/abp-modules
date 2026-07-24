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
        var myGroup = context.AddGroup("MyApp");
        myGroup.AddFeature("MyApp.PdfReporting", defaultValue: "false", valueType: new ToggleStringValueType());
    }
}
```

### Check Features
```csharp
[RequiresFeature("MyApp.PdfReporting")]
public async Task<PdfReportDto> GetPdfReportAsync() { /* ... */ }

if (await _featureChecker.IsEnabledAsync("MyApp.PdfReporting")) { /* ... */ }
```

A notification **definition** in this repo (`NotificationDefinition`, registered through
`INotificationDefinitionProvider`) can similarly declare Feature and Permission requirements,
checked by `INotificationDefinitionManager` at distribution time — see
`framework/common/authorization.md` for how this differs from AppService-level `[Authorize]`.

## Distributed Caching

```csharp
public class BookService : ITransientDependency
{
    private readonly IDistributedCache<BookCacheItem> _cache;

    public async Task<BookCacheItem> GetAsync(Guid bookId)
    {
        return await _cache.GetOrAddAsync(
            bookId.ToString(),
            async () => await GetBookFromDatabaseAsync(bookId),
            () => new DistributedCacheEntryOptions { AbsoluteExpiration = Clock.Now.AddHours(1) });
    }
}

[CacheName("Books")]
public class BookCacheItem { public string Name { get; set; } }
```

## Event Bus

### Local Events (Same Process)
```csharp
public class OrderCreatedEventHandler : ILocalEventHandler<OrderCreatedEvent>, ITransientDependency
{
    public async Task HandleEventAsync(OrderCreatedEvent eventData) { /* same transaction */ }
}

await _localEventBus.PublishAsync(new OrderCreatedEvent { Order = order });
```

### Distributed Events (Cross-Service)
```csharp
[EventName("MyApp.Order.Created")]
public class OrderCreatedEto
{
    public Guid OrderId { get; set; }
}

public class OrderCreatedEtoHandler : IDistributedEventHandler<OrderCreatedEto>, ITransientDependency
{
    public async Task HandleEventAsync(OrderCreatedEto eventData) { /* ... */ }
}

await _distributedEventBus.PublishAsync(new OrderCreatedEto { ... });
```

**This repo's own distributed event is `NotificationDeliveryRequestedEto`** (wire name
`Dignite.Abp.Notifications.NotificationDeliveryRequested`). Core's internal handler adapts transport to the canonical
`INotificationNotifier.DeliverAsync` contract; channel plugins do not implement distributed event handlers. Before
touching it, read `framework/common/notifications-invariants.md` §1 (serialization) and §4 (single-recipient and
cancellation guarantees).

### When to Use Which
- **Local**: Within same module/bounded context
- **Distributed**: Cross-module or cross-Notifier communication (this is how Core reaches every Notifier)

## Background Jobs

```csharp
public class EmailSendingJob : AsyncBackgroundJob<EmailSendingArgs>, ITransientDependency
{
    public override async Task ExecuteAsync(EmailSendingArgs args) { /* ... */ }
}

await _backgroundJobManager.EnqueueAsync(new EmailSendingArgs { ... }, delay: TimeSpan.FromMinutes(5));
```

**This repo's own background job is `NotificationDistributionJob`** — `INotificationPublisher`
enqueues it when the explicit recipient count exceeds the (currently hardcoded)
direct-distribution threshold, instead of distributing inline.

## Localization

### Define Resource
```csharp
[LocalizationResourceName("MyModule")]
public class MyModuleResource { }
```

### Usage
- In `ApplicationService`: Use `L["Key"]` property (already available from base class)
- In other services: Inject `IStringLocalizer<MyResource>`
- This repo localizes a notification's display text **at read time** (per the reader's culture),
  not at publish/distribution time — see `framework/common/application-layer.md`.

> **Tip**: ABP base classes already provide commonly used services as properties. Check before
> injecting: `L`, `Clock`, `CurrentUser`, `CurrentTenant`, `GuidGenerator`, `AuthorizationService`,
> `FeatureChecker`, `DataFilter`, `LoggerFactory`, `Logger`. Plain classes (not inheriting an ABP
> base class) don't get these for free — see `framework/common/abp-core.md`.
