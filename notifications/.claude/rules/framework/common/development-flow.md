---
paths:
  - "**/*AppService*.cs"
  - "**/*Application*/**/*.cs"
  - "**/*Application.Contracts*/**/*.cs"
  - "**/*Dto*.cs"
  - "**/*DbContext*.cs"
  - "**/*.EntityFrameworkCore/**/*.cs"
  - "**/*.MongoDB/**/*.cs"
  - "**/*Permission*.cs"
---

# Development Workflow — Adding a New Aggregate to `NotificationCenter`

> For the far more common case of adding a **new notification type** (no entity involved) or a
> **new Notifier**, see "Adding a feature" in `.claude/rules/template/app.md` — this file is the
> deep-dive for the rarer case of a genuinely new persisted aggregate.

## 1. Domain Layer

Add the entity under `notification-center/src/Dignite.NotificationCenter.Domain/`, at the
namespace-mirrored path (see `template/app.md`'s file layout convention). Match the existing shape
— `BasicAggregateRoot<Guid>`, explicit `IMultiTenant`, protected setters + a protected empty ctor
for the ORM, a public ctor that takes all required state, behavior methods instead of public
setters:

```csharp
public class Widget : BasicAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; protected set; }
    public virtual string Name { get; protected set; } = default!;
    public virtual DateTime CreationTime { get; protected set; }

    protected Widget() { }

    public Widget(Guid id, string name, DateTime creationTime, Guid? tenantId) : base(id)
    {
        Name = name;
        CreationTime = creationTime;
        TenantId = tenantId;
    }

    public virtual void Rename(string name) => Name = name;
}
```

## 2. Domain.Shared

Constants in `NotificationCenter.Domain.Shared` (e.g. max lengths), following
`NotificationCenterConsts`.

## 3. Repository — default to generic, no new interface

Don't add a custom `IWidgetRepository` unless you have a query that can't be expressed via
`IRepository<Widget, Guid>.GetQueryableAsync()`. This repo's convention (see
`framework/data/ef-core.md`) is to put multi-condition queries directly in the consuming service
(as `NotificationStore` does for the existing three aggregates), not behind a bespoke repository
interface.

## 4. EF Core configuration — no migration in this repo

Add the mapping to `NotificationCenterDbContextModelCreatingExtensions.ConfigureNotificationCenter()`:

```csharp
builder.Entity<Widget>(b =>
{
    b.ToTable(NotificationCenterDbProperties.DbTablePrefix + "Widgets", NotificationCenterDbProperties.DbSchema);
    b.ConfigureByConvention();
    b.Property(x => x.Name).IsRequired().HasMaxLength(NotificationCenterConsts.MaxWidgetNameLength);
});
```

Add the equivalent mapping in `Dignite.NotificationCenter.MongoDB` too — both stores back the
same `INotificationStore` abstraction and must stay in sync.

**Do not add a `Migrations/` folder or run `dotnet ef migrations add` in this repo** — there is no
`DbMigrator`. A consuming host app generates its own migration against its own `DbContext` after
picking up the new mapping (either via `NotificationCenterDbContext` directly, or by implementing
`INotificationCenterDbContext` on its own context).

## 5. Application.Contracts

DTOs + service interface, same conventions as generic ABP (`WidgetDto`, `CreateWidgetDto`, etc. —
see `framework/common/application-layer.md`).

## 6. Object Mapping

Check which mapper this repo is using for the layer you're touching before adding a new one
(Mapperly vs AutoMapper) — see `framework/common/application-layer.md`.

## 7. Application Service

Implement against the generic repository, with `[Authorize(...)]` on mutating methods — see
`framework/common/authorization.md`.

## 8. Add Tests

In `notification-center/test/Dignite.NotificationCenter.EntityFrameworkCore.Tests/`, inheriting
`NotificationCenterTestBase`. Use this repo's actual test-naming convention (descriptive sentence,
not `Should_X_When_Y`) — see `framework/testing/patterns.md`.

## Checklist

- [ ] Entity: `BasicAggregateRoot<Guid>` + `IMultiTenant`, protected setters + behavior methods
- [ ] Constants in `Domain.Shared`
- [ ] EF Core mapping in `ConfigureNotificationCenter()` **and** the MongoDB equivalent
- [ ] No migration added in this repo (host app's job)
- [ ] DTOs + service interface in `Application.Contracts`
- [ ] Service implementation with authorization
- [ ] Tests added, following this repo's naming convention
