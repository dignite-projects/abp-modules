# ABP Core Conventions

> **Documentation**: https://abp.io/docs/latest
> **API Reference**: https://abp.io/docs/api/

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

> **Note**: Middleware / request-pipeline configuration (`OnApplicationInitialization`) belongs only in
> the final host application. In this repo that's the **local-dev-only** demo `host/` — the reusable
> module classes under `core/src/` and `file-explorer/src/` should stick to
> `PreConfigureServices`/`ConfigureServices` and never wire up middleware.

## Dependency Injection Conventions

### Automatic Registration
ABP automatically registers services implementing marker interfaces:
- `ITransientDependency` → Transient lifetime
- `ISingletonDependency` → Singleton lifetime
- `IScopedDependency` → Scoped lifetime

Classes inheriting from `ApplicationService`, `DomainService`, `AbpController` are also auto-registered.
The core's upload handlers (`FileSizeLimitHandler`, `FileTypeCheckHandler`, `ImageResizeHandler`) are
plain classes registered as `IFileHandler, ITransientDependency`.

**Before choosing a lifetime, read `framework/common/file-storing-invariants.md` §DI** — a service that
transitively touches a repository/`DbContext` (per-request state) must not be a singleton.

### Repository Usage
Use the generic `IRepository<TEntity, TKey>` for simple CRUD. Define a **custom** repository interface
when you have queries reused across call sites. **This repo does define custom repositories** — its
aggregates each get one because their queries are non-trivial and shared:

```csharp
// IFileDescriptorRepository : IBasicRepository<FileDescriptor, Guid>
//   BlobNameExistsAsync / Md5ExistsAsync / ReferencingAnyAsync / FindByBlobNameAsync /
//   FindByMd5Async / GetListAsync / GetCountAsync  — dedup, uniqueness and filtered listing
// IDirectoryDescriptorRepository : IBasicRepository<DirectoryDescriptor, Guid>
```

They deliberately extend `IBasicRepository<T, Guid>` (not the LINQ-exposing `IRepository<T, Guid>`) and
put every query behind a named method — see `framework/common/ddd-patterns.md` and `framework/data/ef-core.md`.

### Exposing / replacing services
```csharp
[ExposeServices(typeof(IMyService))]
public class MyService : IMyService, ITransientDependency { }
```

The core ships replaceable defaults a consuming app can supersede with
`[Dependency(ReplaceServices = true)]` — e.g. `IBlobNameGenerator` (default `RandomBlobNameGenerator`)
and the per-container, opt-in `IFileDescriptorEntityAuthorizationHandler`. This is the standard ABP
pattern for "a default implementation that a host can replace."

## Important Base Classes

| Base Class | Purpose |
|------------|---------|
| `Entity<TKey>` | Basic entity with ID |
| `AggregateRoot<TKey>` | DDD aggregate root — used by `FileDescriptor` (with `ICreationAuditedObject`/`IDeletionAuditedObject`) |
| `AuditedAggregateRoot<TKey>` | Aggregate root with built-in creation/modification auditing — used by `DirectoryDescriptor` |
| `DomainService` | Domain business logic — `FileDescriptorManager`, `DirectoryManager` |
| `ApplicationService` | Use case orchestration — `FileDescriptorAppService`, `DirectoryDescriptorAppService` |
| `AbpController` | REST API controller |

ABP base classes already inject commonly used services as properties. Before injecting a service, check if it's already available:

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

> **Watch for plain classes that don't inherit any ABP base class** — the core's `IFileHandler`
> implementations and helpers like `RandomBlobNameGenerator` are bare `ITransientDependency` classes, so
> they **inject** `IClock`/`IGuidGenerator`/`ICurrentTenant`/`IStringLocalizer<T>` via their constructors
> rather than using base-class properties they don't have. Don't "simplify" that to `Clock`/`GuidGenerator`
> property access — those properties only exist on `ApplicationService`/`DomainService`/`AbpController`.

## Async Best Practices
- Use async all the way - never use `.Result` or `.Wait()`
- All async methods should end with `Async` suffix
- ABP automatically handles `CancellationToken` in most cases (e.g., from `HttpContext.RequestAborted`)
- Flow `CancellationToken` through stream copies, blob I/O and image decoding — the custom repository
  methods here already take one, and the audit found several I/O paths that dropped it. Pass it on.

## Time Handling
Never use `DateTime.Now` or `DateTime.UtcNow` directly. Use ABP's `IClock` service:

```csharp
// In classes inheriting from base classes (ApplicationService, DomainService, etc.)
public class FileDescriptorAppService : ApplicationService
{
    public void DoSomething()
    {
        var now = Clock.Now; // ✅ Already available as property
    }
}

// In other services (e.g. an IFileHandler) - inject IClock
public class MyHandler : IFileHandler, ITransientDependency
{
    private readonly IClock _clock;

    public MyHandler(IClock clock) => _clock = clock;

    public Task ExecuteAsync(FileHandlerContext context)
    {
        var now = _clock.Now; // ✅ Correct
        // var now = DateTime.Now; // ❌ Wrong - not testable, ignores timezone settings
        return Task.CompletedTask;
    }
}
```

## Business Exceptions
Use `BusinessException` for domain rule violations with namespaced error codes. This repo keeps error codes
in `FileExplorerErrorCodes` / `FileErrorCodes` and maps their namespace to the localization resource:

```csharp
throw new BusinessException(FileExplorerErrorCodes.Directories.DirectoryNotExist)
    .WithData("Name", name);
```

Configure localization mapping (the error-code namespace **must** match the resource key namespace — a
mismatch there was a real bug, fixed in "align file explorer error localization"):
```csharp
Configure<AbpExceptionLocalizationOptions>(options =>
{
    options.MapCodeNamespace("Dignite.FileExplorer", typeof(FileExplorerResource));
});
```

## Localization
- In base classes (`ApplicationService`, `AbpController`, etc.): Use `L["Key"]` - this is the `IStringLocalizer` property
- In other services: Inject `IStringLocalizer<TResource>`
- Always localize user-facing messages and exceptions

**Localization file location**: `*.Domain.Shared/Localization/{ResourceName}/{lang}.json`
(here: `Dignite.FileExplorer.Domain.Shared/Dignite/FileExplorer/Localization/Resources/*.json`).

## ❌ Never Use (ABP Anti-Patterns)

| Don't Use | Use Instead |
|-----------|-------------|
| Minimal APIs | ABP Controllers or Auto API Controllers |
| MediatR | Application Services / domain events |
| `DbContext` directly in App Services | `IRepository<T>` / the custom repositories |
| `AddScoped/AddTransient/AddSingleton` | `ITransientDependency`, `ISingletonDependency` |
| `DateTime.Now` | `IClock` / `Clock.Now` |
| Custom UnitOfWork | ABP's `IUnitOfWorkManager` |
| Hardcoded role checks | Permission-based authorization |
| Business logic in Controllers | Application Services |
| Trusting the client-supplied MIME type / extension for validation | Detect the real content (`FileTypeCheckHandler`, image-format sniffing) — see `file-storing-invariants.md` |
| Buffering the whole upload into memory before enforcing the size limit | Enforce size at the HTTP layer and while streaming — see `file-storing-invariants.md` |
