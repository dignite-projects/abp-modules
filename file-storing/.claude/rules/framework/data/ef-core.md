---
paths:
  - "**/*.EntityFrameworkCore/**/*.cs"
  - "**/EntityFrameworkCore/**/*.cs"
  - "**/*DbContext*.cs"
---

# EF Core in This Repo — Module DbContext, Not App DbContext

> **Docs**: https://abp.io/docs/latest/framework/data/entity-framework-core

The `file-explorer` library projects ship **no migrations**. `FileExplorer.EntityFrameworkCore` is a *module*
integration: it defines the entity mappings, the custom repository implementations, and an interface a host
app's own `DbContext` can implement — but the host generates and owns the actual migration. Don't add a
`Migrations/` folder under `file-explorer/src/`. (The demo `host/` has its own `Migrations/`; that's the host's,
not the module's.)

## The module DbContext pattern actually used here

```csharp
// IFileExplorerDbContext.cs — the seam a host app's own DbContext can implement directly
[ConnectionStringName(FileExplorerDbProperties.ConnectionStringName)] // "FileExplorer"
public interface IFileExplorerDbContext : IEfCoreDbContext
{
    DbSet<DirectoryDescriptor> DirectoryDescriptors { get; }
    DbSet<FileDescriptor> FileDescriptors { get; }
}

// FileExplorerDbContext.cs — standalone default implementation
[ConnectionStringName(FileExplorerDbProperties.ConnectionStringName)]
public class FileExplorerDbContext : AbpDbContext<FileExplorerDbContext>, IFileExplorerDbContext
{
    public DbSet<DirectoryDescriptor> DirectoryDescriptors { get; set; }
    public DbSet<FileDescriptor> FileDescriptors { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureFileExplorer();   // <- the extension a host DbContext also calls
    }
}
```

Note there is **no** `IHasEventInbox`/`IHasEventOutbox` here — the modules don't route distributed events, so
the DbContext carries no outbox/inbox tables. Don't add them.

## The entity configuration + this repo's real indexes

```csharp
public static class FileExplorerDbContextModelCreatingExtensions
{
    public static void ConfigureFileExplorer(this ModelBuilder builder)
    {
        builder.Entity<FileDescriptor>(b =>
        {
            b.ToTable(FileExplorerDbProperties.DbTablePrefix + "FileDescriptors", FileExplorerDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(q => q.ContainerName).IsRequired().HasMaxLength(FileConsts.MaxContainerNameLength);
            b.Property(q => q.BlobName).IsRequired().HasMaxLength(FileConsts.MaxBlobNameLength);

            // Unique blob name per (tenant, container) — a hard invariant, see file-storing-invariants.md
            b.HasIndex(q => new { q.TenantId, q.ContainerName, q.BlobName }).IsUnique();
            // Content dedup: unique MD5 per (tenant, container), filtered so empty MD5 rows don't collide
            b.HasIndex(q => new { q.TenantId, q.ContainerName, q.Md5 })
                .IsUnique().HasFilter($"{nameof(FileDescriptor.Md5)} <> ''");
            // Reference dedup + associated-entity + listing lookups
            b.HasIndex(q => new { q.TenantId, q.ContainerName, q.ReferBlobName });
            b.HasIndex(q => new { q.TenantId, q.ContainerName, q.EntityId });
            b.HasIndex(q => new { q.TenantId, q.ContainerName, q.CreationTime, q.CreatorId, q.DirectoryId });

            b.ApplyObjectExtensionMappings();
        });
        // DirectoryDescriptor: index (TenantId, ContainerName, CreatorId, ParentId)
    }
}
```

When adding a field or entity: edit `ConfigureFileExplorer()` **and the MongoDB equivalent**
(`FileExplorerMongoDbContextExtensions.ConfigureFileExplorer`) — do **not** add a migration in the module. A
consuming host adds its own EF Core migration after upgrading the package.

## Table prefix / schema

Use `FileExplorerDbProperties.DbTablePrefix` (`"Fe"`) / `.DbSchema` (`null`) — don't hardcode table or
collection names. The MongoDB collections use the same prefix (`FeFileDescriptors`, `FeDirectoryDescriptors`).

## Repositories: custom, registered explicitly

This repo defines **custom** repositories and registers them by hand — it does **not** rely on
`AddDefaultRepositories` generic repositories for its aggregates:

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.AddAbpDbContext<FileExplorerDbContext>(options =>
    {
        options.AddRepository<DirectoryDescriptor, EfCoreDirectoryDescriptorRepository>();
        options.AddRepository<FileDescriptor, EfCoreFileDescriptorRepository>();
    });
}
```

`EfCoreFileDescriptorRepository : EfCoreRepository<IFileExplorerDbContext, FileDescriptor, Guid>,
IFileDescriptorRepository` — the query methods (`BlobNameExistsAsync`, `Md5ExistsAsync`, `ReferencingAnyAsync`,
`FindByBlobNameAsync`, `FindByMd5Async`, `GetListAsync`, `GetCountAsync`) live here, and the **MongoDB project
implements the same interface**. Adding a query means adding it to the interface and to **both** providers.

## Keep EF Core and MongoDB in sync

Both providers back the same repository interfaces, so their behavior must match:
- **Indexes**: the audit found MongoDB shipped without the business indexes EF Core has. When you add/adjust an
  EF index, add the matching `CreateIndex` on the Mongo side.
- **Default sort order**: EF and Mongo must return the same default ordering (list queries default to
  `CreationTime` descending — see `GetListAsync_ShouldOrderByCreationTimeDescendingByDefault`). A provider-only
  sort tweak is a bug.
- **Sorting whitelist**: dynamic-LINQ `sorting` strings must be validated against an allowlist of columns —
  don't pass raw client sort input to the query (fixed in "allowlist file descriptor sorting").

## Testing

Integration tests run against EF Core's **Sqlite in-memory** provider (`Volo.Abp.EntityFrameworkCore.Sqlite`,
via the test module) — no real SQL Server/migration needed to run `dotnet test`. The same abstract scenarios
run against MongoDB via an embedded mongod. See `framework/testing/patterns.md`.

## General EF Core best practices (still apply)

- Always call `b.ConfigureByConvention()` inside entity configuration.
- Add explicit indexes for the fields you actually query by — check the real access pattern (this repo's
  indexes are built around per-container blob-name uniqueness, MD5/reference dedup, and the
  `(TenantId, ContainerName, CreationTime, CreatorId, DirectoryId)` listing query).
- Use `AsNoTracking()` for read-only queries; flow `CancellationToken` through — the audit found several query
  paths that ignored it.
