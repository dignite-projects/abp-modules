---
paths:
  - "**/*.EntityFrameworkCore/**/*.cs"
  - "**/EntityFrameworkCore/**/*.cs"
  - "**/*DbContext*.cs"
---

# EF Core in This Repo — Module DbContext, Not App DbContext

> **Docs**: https://abp.io/docs/latest/framework/data/entity-framework-core

This repo has **no `DbMigrator` and ships no migrations**. `NotificationCenter.EntityFrameworkCore`
is a *module* integration: it defines the entity mappings and an interface a host app's own
`DbContext` can implement, but the host generates and owns the actual migration. Don't go looking
for (or add) a `Migrations/` folder here — there isn't meant to be one.

## The module DbContext pattern actually used here

```csharp
// INotificationCenterDbContext.cs — the seam a host app's own DbContext can implement directly
[ConnectionStringName(NotificationCenterDbProperties.ConnectionStringName)]
public interface INotificationCenterDbContext : IEfCoreDbContext
{
    DbSet<Notification> Notifications { get; }
    DbSet<UserNotification> UserNotifications { get; }
    DbSet<NotificationSubscription> NotificationSubscriptions { get; }
}

// NotificationCenterDbContext.cs — standalone default implementation, used when the host
// doesn't want to fold these tables into an existing DbContext
[ConnectionStringName(NotificationCenterDbProperties.ConnectionStringName)]
public class NotificationCenterDbContext :
    AbpDbContext<NotificationCenterDbContext>, INotificationCenterDbContext,
    IHasEventInbox, IHasEventOutbox   // transactional outbox/inbox — see below
{
    public DbSet<Notification> Notifications { get; set; } = default!;
    public DbSet<UserNotification> UserNotifications { get; set; } = default!;
    public DbSet<NotificationSubscription> NotificationSubscriptions { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureEventInbox();
        builder.ConfigureEventOutbox();
        builder.ConfigureNotificationCenter();   // <- the extension a host DbContext also calls
    }
}
```

```csharp
// NotificationCenterDbContextModelCreatingExtensions.cs — the actual entity configuration
public static class NotificationCenterDbContextModelCreatingExtensions
{
    public static void ConfigureNotificationCenter(this ModelBuilder builder)
    {
        builder.Entity<Notification>(b =>
        {
            b.ToTable(NotificationCenterDbProperties.DbTablePrefix + "Notifications", NotificationCenterDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.NotificationName).IsRequired().HasMaxLength(NotificationCenterConsts.MaxNotificationNameLength);
            b.HasIndex(x => new { x.TenantId, x.NotificationName, x.CreationTime });
        });
        // ...UserNotification, NotificationSubscription follow the same shape
    }
}
```

When adding a field or entity: edit `ConfigureNotificationCenter()` (and the MongoDB project's
equivalent mapping) — do **not** add a migration in this repo. A consuming host app adds its own EF
Core migration after upgrading the package, exactly as it would for any other ABP module.

## Table prefix / schema

Use `NotificationCenterDbProperties.DbTablePrefix` / `.DbSchema` (mirrors the generic
`MyProjectConsts.DbTablePrefix` pattern from ABP's app template) — don't hardcode table names.

## Repositories: generic only, queries live in `INotificationStore`

This repo does **not** define custom per-aggregate repository interfaces (no `INotificationRepository`
etc.). The EF Core project registers generic repositories and all querying — including multi-field
filters — is written directly against `IRepository<T, Guid>.GetQueryableAsync()` /
`IAsyncQueryableExecuter` inside the `INotificationStore` implementation (`NotificationStore.cs`),
which is the actual abstraction boundary for this module. If you're adding a query, it almost
certainly belongs as a method on `INotificationStore`, not a new repository interface. See
`framework/common/ddd-patterns.md` for when a custom repository interface would still be
appropriate elsewhere in this repo.

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.AddAbpDbContext<NotificationCenterDbContext>(options =>
    {
        // NOTE: this project currently passes includeAllEntities: true even though all three
        // entities are aggregate roots (which would get repositories anyway without the flag).
        // If you add a genuinely non-aggregate-root child entity later, revisit this — the
        // general ABP guidance is to avoid includeAllEntities: true so child entities can't get
        // a repository that bypasses their aggregate root.
        options.AddDefaultRepositories(includeAllEntities: true);
    });
}
```

## Transactional outbox/inbox (`IHasEventInbox` / `IHasEventOutbox`)

`NotificationCenterDbContext` always defines the outbox/inbox tables
(`IncomingEventRecord`/`OutgoingEventRecord`); whether an app actually routes distributed events
through them is an ABP-level, app-side opt-in. This is what makes "persist the notification" +
"publish `NotificationDeliveryRequestedEto`" atomic — see invariant §1/§5 in `notifications-invariants.md`.
Don't remove these interfaces when touching the DbContext.

## Testing

Integration tests run against EF Core's **Sqlite in-memory** provider (`Volo.Abp.EntityFrameworkCore.Sqlite`,
via the test module) — no real SQL Server/migration needed to run `dotnet test`. See
`framework/testing/patterns.md`.

## General EF Core best practices (still apply)

- Always call `b.ConfigureByConvention()` inside entity configuration.
- Add explicit indexes for the fields you actually query by — check the real access pattern, not
  just "the obvious" column (this repo's indexes were specifically redesigned around
  `(TenantId, UserId, State, CreationTime)` for the inbox query and
  `(TenantId, NotificationName, EntityTypeName, EntityId)` for subscription lookup).
- Use `AsNoTracking()` / the async query executer for read-only queries; avoid N+1 by batching
  (see `NotificationStore.GetUserNotificationsAsync` for the two-query-plus-in-memory-join pattern
  used to keep the same store logic portable to MongoDB).
