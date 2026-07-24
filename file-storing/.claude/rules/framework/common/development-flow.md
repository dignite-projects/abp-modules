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

# Development Workflow — Adding a New Aggregate to `file-explorer`

> For the far more common case of adding a **new upload rule/transform** (a new `IFileHandler`, no entity
> involved), see "Adding a feature" in `.claude/rules/template/app.md` — this file is the deep-dive for the
> rarer case of a genuinely new persisted aggregate.

## 1. Domain Layer

Add the entity under `file-explorer/src/Dignite.FileExplorer.Domain/`, at the namespace-mirrored path (see
`template/app.md`'s file-layout convention). Match the existing shape — an audited aggregate root + explicit
`IMultiTenant`, protected setters on business properties, a protected empty ctor for the ORM, a public ctor
that takes all required state, and behavior methods:

```csharp
public class Widget : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public string ContainerName { get; protected set; } = default!;
    public string Name { get; protected set; } = default!;
    public Guid? TenantId { get; protected set; }

    protected Widget() { }

    public Widget(Guid id, string containerName, string name, Guid? tenantId) : base(id)
    {
        ContainerName = containerName;
        Name = name;
        TenantId = tenantId;
    }

    public void Rename(string name)
        => Name = Check.Length(name, nameof(name), WidgetConsts.MaxNameLength) ?? string.Empty;
}
```

## 2. Domain.Shared

Constants (max lengths, error codes) in `FileExplorer.Domain.Shared`, following `FileDescriptorConsts` /
`DirectoryDescriptorConsts` and `FileExplorerErrorCodes`. Keep error-code namespaces aligned with the
localization resource keys (a mismatch there was a real bug).

## 3. Repository — add a custom interface (this repo's convention)

Unlike the generic ABP default, this repo puts queries behind **custom repository interfaces**. Add
`IWidgetRepository : IBasicRepository<Widget, Guid>` in the Domain layer with named query methods, and
implement it in **both** data projects (`EfCoreWidgetRepository`, `MongoWidgetRepository`). Register each in its
module:

```csharp
// FileExplorerEntityFrameworkCoreModule.ConfigureServices
options.AddRepository<Widget, EfCoreWidgetRepository>();
```

Only fall back to the generic `IRepository<Widget, Guid>` for a genuinely trivial CRUD entity with no reused
queries.

## 4. EF Core & MongoDB configuration — no migration in this repo

Add the EF mapping to `FileExplorerDbContextModelCreatingExtensions.ConfigureFileExplorer()`:

```csharp
builder.Entity<Widget>(b =>
{
    b.ToTable(FileExplorerDbProperties.DbTablePrefix + "Widgets", FileExplorerDbProperties.DbSchema);
    b.ConfigureByConvention();
    b.Property(x => x.Name).IsRequired().HasMaxLength(WidgetConsts.MaxNameLength);
    b.HasIndex(x => new { x.TenantId, x.ContainerName, x.Name });
    b.ApplyObjectExtensionMappings();
});
```

Add the equivalent mapping in `FileExplorerMongoDbContextExtensions.ConfigureFileExplorer()` — both stores back
the same repository interfaces and must stay in sync.

**Do not add a `Migrations/` folder to `file-explorer/src/`** — the library projects ship no migrations. A
consuming host (including the demo `host/`, which *does* have its own `Migrations/`) generates and owns the
migration after picking up the new mapping. See `framework/data/ef-core.md`.

## 5. Application.Contracts

DTOs + service interface + any new permission (`FileExplorerPermissions`), same conventions as generic ABP —
see the repo-root `.claude/rules/framework/common/application-layer.md` and the repo-root `.claude/rules/framework/common/authorization.md`.

## 6. Object Mapping — Mapperly

Add a `MapperBase<Widget, WidgetDto>` partial to `FileExplorerApplicationMappers.cs`
(`[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]`); `[MapperIgnoreTarget]` any computed
field you fill in the AppService. Don't reach for AutoMapper — see `application-layer.md`.

## 7. Application Service

Implement against the custom repository, with `[Authorize(...)]` for coarse permissions and resource-based
`AuthorizationService` checks for per-resource access — see the repo-root `.claude/rules/framework/common/authorization.md`.

## 8. Add Tests

Put provider-agnostic scenarios as **abstract** `Widget…_Tests<TStartupModule>` classes in
`file-explorer/test/Dignite.FileExplorer.TestBase`, then inherit them from both the EF Core and MongoDB test
projects so they run on both providers. Follow this repo's naming convention (`Method_ShouldX_WhenY`) — see
`framework/testing/patterns.md`.

## Checklist

- [ ] Entity: audited aggregate root + `IMultiTenant`, protected setters + behavior methods
- [ ] Constants + error codes in `Domain.Shared` (namespaces aligned with localization)
- [ ] Custom repository interface + EF Core **and** MongoDB implementations, both registered
- [ ] EF mapping in `ConfigureFileExplorer()` **and** the MongoDB equivalent
- [ ] No migration added in `file-explorer/src/` (the host's job)
- [ ] DTOs + service interface + permissions in `Application.Contracts`
- [ ] Mapperly map in `FileExplorerApplicationMappers`
- [ ] Service implementation with permission + resource-based authorization
- [ ] Abstract cross-provider tests in `TestBase`, inherited by EF Core + MongoDB projects
