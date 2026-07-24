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

> **Note**: Middleware configuration (`OnApplicationInitialization`) should only be done in the final host application, not in reusable modules. This repo has no host application — every module class here should stick to `PreConfigureServices`/`ConfigureServices`.

## Dependency Injection Conventions

### Automatic Registration
ABP automatically registers services implementing marker interfaces:
- `ITransientDependency` → Transient lifetime
- `ISingletonDependency` → Singleton lifetime
- `IScopedDependency` → Scoped lifetime

Classes inheriting from `ApplicationService`, `DomainService`, `AbpController` are also auto-registered.

**Before choosing a lifetime, read `framework/common/notifications-invariants.md` §2** — this
repo's motivating bug class was a singleton capturing a scoped dependency.

### Repository Usage
You can use the generic `IRepository<TEntity, TKey>` for simple CRUD operations. Define custom repository interfaces only when you need custom query methods reused across call sites — this repo's own aggregates (`Notification`, `UserNotification`, `NotificationSubscription`) deliberately use only the generic repository; see `framework/common/ddd-patterns.md`.

```csharp
// Simple CRUD - Generic repository is fine
public class BookAppService : ApplicationService
{
    private readonly IRepository<Book, Guid> _bookRepository; // ✅ OK for simple operations
}
```

### Exposing Services
```csharp
[ExposeServices(typeof(IMyService))]
public class MyService : IMyService, ITransientDependency { }
```

This repo uses `[Dependency(ReplaceServices = true)] [ExposeServices(typeof(INotificationStore))]`
on `NotificationStore` to replace the core's `NullNotificationStore` once `NotificationCenter` is
installed — the standard ABP pattern for "an optional module supersedes a default no-op
implementation."

## Important Base Classes

| Base Class | Purpose |
|------------|---------|
| `Entity<TKey>` | Basic entity with ID |
| `AggregateRoot<TKey>` | DDD aggregate root |
| `BasicAggregateRoot<TKey>` | Leaner aggregate root without ABP's local-event collection overhead — used by all three entities in `NotificationCenter.Domain` |
| `DomainService` | Domain business logic |
| `ApplicationService` | Use case orchestration |
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

> **Watch for plain classes that don't inherit any ABP base class** — e.g. this repo's
> `NotificationStore : INotificationStore, ITransientDependency` is a bare class, so it correctly
> **injects** `IClock`/`IGuidGenerator`/`ICurrentTenant` via its constructor rather than using base-class
> properties it doesn't have. Don't "simplify" that to `Clock`/`GuidGenerator` property access — those
> properties only exist on `ApplicationService`/`DomainService`/`AbpController`.

## Async Best Practices
- Use async all the way - never use `.Result` or `.Wait()`
- All async methods should end with `Async` suffix
- ABP automatically handles `CancellationToken` in most cases (e.g., from `HttpContext.RequestAborted`)
- Only pass `CancellationToken` explicitly when implementing custom cancellation logic

## Time Handling
Never use `DateTime.Now` or `DateTime.UtcNow` directly. Use ABP's `IClock` service:

```csharp
// In classes inheriting from base classes (ApplicationService, DomainService, etc.)
public class BookAppService : ApplicationService
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
throw new BusinessException("MyModule:BookNameAlreadyExists")
    .WithData("Name", bookName);
```

Configure localization mapping:
```csharp
Configure<AbpExceptionLocalizationOptions>(options =>
{
    options.MapCodeNamespace("MyModule", typeof(MyModuleResource));
});
```

## Localization
- In base classes (`ApplicationService`, `AbpController`, etc.): Use `L["Key"]` - this is the `IStringLocalizer` property
- In other services: Inject `IStringLocalizer<TResource>`
- Always localize user-facing messages and exceptions

**Localization file location**: `*.Domain.Shared/Localization/{ResourceName}/{lang}.json`

## ❌ Never Use (ABP Anti-Patterns)

| Don't Use | Use Instead |
|-----------|-------------|
| Minimal APIs | ABP Controllers or Auto API Controllers |
| MediatR | Application Services / domain events |
| `DbContext` directly in App Services | `IRepository<T>` |
| `AddScoped/AddTransient/AddSingleton` | `ITransientDependency`, `ISingletonDependency` |
| `DateTime.Now` | `IClock` / `Clock.Now` |
| Custom UnitOfWork | ABP's `IUnitOfWorkManager` |
| Hardcoded role checks | Permission-based authorization |
| Business logic in Controllers | Application Services |
| CLR type name / `AssemblyQualifiedName` as a wire discriminator | Stable `[NotificationDataType]` discriminator — see `notifications-invariants.md` §1 |
