# ABP Core Conventions

> **Documentation**: https://abp.io/docs/latest
> **API Reference**: https://abp.io/docs/api/
>
> **No `paths:` frontmatter, so this always loads.** Generic ABP conventions first, then a
> per-module section for each of `file-storing` and `notifications`. Read the generic part for "how
> ABP works" and your module's section for "how *this* module does it".

## Module System
Every ABP application/module has a module class that configures services:

```csharp
[DependsOn(
    typeof(AbpDddDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class MyAppModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Service registration and configuration
    }
}
```

> **Note**: Middleware / request-pipeline configuration (`OnApplicationInitialization`) belongs only in the
> final host application. In this repo that means each module's **local-dev-only** demo `host/` — the reusable
> module classes under `core/src/`, `file-explorer/src/`, and `notification-center/src/` should stick to
> `PreConfigureServices`/`ConfigureServices` and never wire up middleware.

## Dependency Injection Conventions

### Automatic Registration
ABP automatically registers services implementing marker interfaces:
- `ITransientDependency` → Transient lifetime
- `ISingletonDependency` → Singleton lifetime
- `IScopedDependency` → Scoped lifetime

Classes inheriting from `ApplicationService`, `DomainService`, `AbpController` are also auto-registered.

**Before choosing a lifetime, read your module's invariants file** — `file-storing-invariants.md` §7 or
`notifications-invariants.md` §2. Both modules encode the same hard rule for their own reasons: a service that
transitively touches a repository/`DbContext` (per-request state) must **not** be a singleton. Autofac won't
fail this at startup; it fails under concurrent load with thread-unsafe `DbContext` use.

### Repository Usage
Use the generic `IRepository<TEntity, TKey>` for simple CRUD. Define a **custom** repository interface when you
have queries reused across call sites.

**The two modules deliberately answer this differently** — check the module you're editing before adding a
query:

| Module | Convention |
|---|---|
| `file-storing` | **Custom repositories.** `IFileDescriptorRepository` / `IDirectoryDescriptorRepository` extend `IBasicRepository<T, Guid>` and put every query behind a named method. |
| `notifications` | **Generic repository only.** All querying goes through `IRepository<T, Guid>` inside the `INotificationStore` implementation; no custom per-aggregate interfaces. |

See the module's `framework/common/ddd-patterns.md` and `framework/data/ef-core.md`.

### Exposing / replacing services
```csharp
[ExposeServices(typeof(IMyService))]
public class MyService : IMyService, ITransientDependency { }
```

Both modules ship replaceable defaults a consuming app (or an optional companion module) can supersede with
`[Dependency(ReplaceServices = true)]` — the standard ABP pattern for "a default implementation that a host can
replace." See the per-module sections below for which seams exist.

## Important Base Classes

| Base Class | Purpose |
|------------|---------|
| `Entity<TKey>` | Basic entity with ID |
| `AggregateRoot<TKey>` | DDD aggregate root |
| `BasicAggregateRoot<TKey>` | Leaner aggregate root without ABP's local-event collection overhead |
| `AuditedAggregateRoot<TKey>` | Aggregate root with built-in creation/modification auditing |
| `DomainService` | Domain business logic (`*Manager` suffix) |
| `ApplicationService` | Use case orchestration |
| `AbpController` | REST API controller |

Which base class each module's aggregates actually use is a deliberate, differing choice — see the per-module
sections below and `framework/common/ddd-patterns.md`.

ABP base classes already inject commonly used services as properties. Before injecting a service, check if it's
already available:

| Property | Available In | Description |
|----------|--------------|--------------|
| `GuidGenerator` | All base classes | Generate GUIDs |
| `Clock` | All base classes | Current time (use instead of `DateTime`) |
| `CurrentUser` | All base classes | Authenticated user info |
| `CurrentTenant` | All base classes | Multi-tenancy context |
| `L` (StringLocalizer) | `ApplicationService`, `AbpController` | Localization |
| `AuthorizationService` | `ApplicationService`, `AbpController` | Permission checks |
| `FeatureChecker` | `ApplicationService`, `AbpController` | Feature availability |
| `DataFilter` | All base classes | Data filtering (soft-delete, tenant) |
| `UnitOfWorkManager` | `ApplicationService`, `DomainService` | Unit of work management |
| `LoggerFactory` | All base classes | Create loggers |
| `Logger` | All base classes | Logging (auto-created) |
| `LazyServiceProvider` | All base classes | Lazy service resolution |

**Useful methods from base classes:**
- `CheckPolicyAsync()` - Check permission and throw if not granted
- `IsGrantedAsync()` - Check permission without throwing

> **Watch for plain classes that don't inherit any ABP base class.** Both modules have them — they **inject**
> `IClock`/`IGuidGenerator`/`ICurrentTenant`/`IStringLocalizer<T>` via their constructors rather than using
> base-class properties they don't have. Don't "simplify" that to `Clock`/`GuidGenerator` property access —
> those properties only exist on `ApplicationService`/`DomainService`/`AbpController`.

## Async Best Practices
- Use async all the way - never use `.Result` or `.Wait()`
- All async methods should end with `Async` suffix
- ABP automatically handles `CancellationToken` in most cases (e.g., from `HttpContext.RequestAborted`)
- **Flow `CancellationToken` through I/O explicitly.** Both modules treat dropped cancellation as a defect —
  see each module's invariants file for the specific paths that must carry it.

## Time Handling
Never use `DateTime.Now` or `DateTime.UtcNow` directly. Use ABP's `IClock` service:

```csharp
// In classes inheriting from base classes (ApplicationService, DomainService, etc.)
public class SomeAppService : ApplicationService
{
    public void DoSomething()
    {
        var now = Clock.Now; // ✅ Already available as property
    }
}

// In other services - inject IClock
public class MyService : ITransientDependency
{
    private readonly IClock _clock;

    public MyService(IClock clock) => _clock = clock;

    public void DoSomething()
    {
        var now = _clock.Now; // ✅ Correct
        // var now = DateTime.Now; // ❌ Wrong - not testable, ignores timezone settings
    }
}
```

## Business Exceptions
Use `BusinessException` for domain rule violations with namespaced error codes:

```csharp
throw new BusinessException("MyModule:SomethingWentWrong")
    .WithData("Name", name);
```

Configure localization mapping — the error-code namespace **must** match the resource key namespace:
```csharp
Configure<AbpExceptionLocalizationOptions>(options =>
{
    options.MapCodeNamespace("MyModule", typeof(MyModuleResource));
});
```

A mismatch there means business exceptions silently don't localize.

## Localization
- In base classes (`ApplicationService`, `AbpController`, etc.): Use `L["Key"]` - this is the `IStringLocalizer` property
- In other services: Inject `IStringLocalizer<TResource>`
- Always localize user-facing messages and exceptions

**Localization file location**: `*.Domain.Shared/Localization/{ResourceName}/{lang}.json` (each module mirrors
its own namespace path).

## ❌ Never Use (ABP Anti-Patterns)

| Don't Use | Use Instead |
|-----------|-------------|
| Minimal APIs | ABP Controllers or Auto API Controllers |
| MediatR | Application Services / domain events |
| `DbContext` directly in App Services | `IRepository<T>` / the module's repository convention |
| `AddScoped/AddTransient/AddSingleton` | `ITransientDependency`, `ISingletonDependency` |
| `DateTime.Now` | `IClock` / `Clock.Now` |
| Custom UnitOfWork | ABP's `IUnitOfWorkManager` |
| Hardcoded role checks | Permission-based authorization |
| Business logic in Controllers | Application Services |

---

## In `file-storing`

### DI lifetimes
The core's upload handlers (`FileSizeLimitHandler`, `FileTypeCheckHandler`, `ImageResizeHandler`) are plain
classes registered as `IFileHandler, ITransientDependency`. See `file-storing-invariants.md` §7.

### Repositories — custom
```csharp
// IFileDescriptorRepository : IBasicRepository<FileDescriptor, Guid>
//   BlobNameExistsAsync / Md5ExistsAsync / ReferencingAnyAsync / FindByBlobNameAsync /
//   FindByMd5Async / GetListAsync / GetCountAsync  — dedup, uniqueness and filtered listing
// IDirectoryDescriptorRepository : IBasicRepository<DirectoryDescriptor, Guid>
```
They deliberately extend `IBasicRepository<T, Guid>` (not the LINQ-exposing `IRepository<T, Guid>`). Add a query
to the interface **and** to both the EF Core and MongoDB implementations.

### Replaceable defaults
`IBlobNameGenerator` (default `RandomBlobNameGenerator`) and the per-container, opt-in
`IFileDescriptorEntityAuthorizationHandler`.

### Base classes used
| Base Class | Used by |
|------------|---------|
| `AggregateRoot<TKey>` | `FileDescriptor` (with `ICreationAuditedObject`/`IDeletionAuditedObject`) |
| `AuditedAggregateRoot<TKey>` | `DirectoryDescriptor` |
| `DomainService` | `FileDescriptorManager`, `DirectoryManager` |
| `ApplicationService` | `FileDescriptorAppService`, `DirectoryDescriptorAppService` |

> **Plain classes**: the `IFileHandler` implementations and helpers like `RandomBlobNameGenerator` inherit no
> ABP base class:
> ```csharp
> public class MyHandler : IFileHandler, ITransientDependency
> {
>     private readonly IClock _clock;
>     public MyHandler(IClock clock) => _clock = clock;   // ✅ no base-class `Clock` property here
> }
> ```

### Cancellation
Flow `CancellationToken` through stream copies, blob I/O and image decoding — the custom repository methods
already take one, and the audit found several I/O paths that dropped it. See `file-storing-invariants.md` §8.

### Error codes & localization
Error codes live in `FileExplorerErrorCodes` / `FileErrorCodes`, and their namespace **must** match the
localization resource key namespace:

```csharp
throw new BusinessException(FileExplorerErrorCodes.Directories.DirectoryNotExist).WithData("Name", name);

options.MapCodeNamespace("Dignite.FileExplorer", typeof(FileExplorerResource));
```

A mismatch there was a real bug, fixed in "align file explorer error localization". Localization files:
`Dignite.FileExplorer.Domain.Shared/Dignite/FileExplorer/Localization/Resources/*.json`.

### Extra anti-patterns
| Don't Use | Use Instead |
|-----------|-------------|
| Trusting the client-supplied MIME type / extension for validation | Detect the real content (`FileTypeCheckHandler`, image-format sniffing) — `file-storing-invariants.md` §2 |
| Buffering the whole upload into memory before enforcing the size limit | Enforce size at the HTTP layer and while streaming — `file-storing-invariants.md` §1 |

---

## In `notifications`

### DI lifetimes — this module's motivating bug
The legacy `UserNotificationManager` was `ISingletonDependency` while injecting `INotificationStore` (which,
with Center installed, holds repositories/`DbContext`). Definition/registry caches (name → definition lookups)
are fine as singletons — the permission/store checks that ride along with them are not. See
`notifications-invariants.md` §2.

### Repositories — generic only
The three aggregates (`Notification`, `UserNotification`, `NotificationSubscription`) use **only** the generic
`IRepository<TEntity, TKey>`. New queries go on `INotificationStore`, not behind a new repository interface.

### Replacing the store
```csharp
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(INotificationStore))]
public class NotificationStore : INotificationStore, ITransientDependency { }
```
This replaces Core's `NullNotificationStore` once `NotificationCenter` is installed.

### Base classes used
| Base Class | Used by |
|------------|---------|
| `BasicAggregateRoot<TKey>` | All three `NotificationCenter.Domain` entities — leaner than `AggregateRoot<TKey>`, no local-event collection overhead |

> **Plain classes**: `NotificationStore : INotificationStore, ITransientDependency` inherits no ABP base class,
> so it correctly **injects** `IClock`/`IGuidGenerator`/`ICurrentTenant` via its constructor.

### Extra anti-patterns
| Don't Use | Use Instead |
|-----------|-------------|
| CLR type name / `AssemblyQualifiedName` as a wire discriminator | A stable `[NotificationDataType]` discriminator — `notifications-invariants.md` §1 |
| Newtonsoft.Json anywhere in the notification pipeline | System.Text.Json only, through `INotificationDataSerializer` |
| A polymorphic/abstract member on an ETO | A flat, default-STJ-round-trippable POCO (`DataJson`) — `notifications-invariants.md` §1 |
