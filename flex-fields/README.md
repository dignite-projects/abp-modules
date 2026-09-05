# Dignite.Abp.FlexFields

> Part of [**dignite-projects/abp-modules**](https://github.com/dignite-projects/abp-modules) — see
> the [repository README](../README.md) for the other modules, and
> [CONTRIBUTING.md](../CONTRIBUTING.md) for the build, versioning, and release process shared across
> them.

A **constraint kernel** for adding flexible, per-instance fields (EAV) to any host entity in an
**[ABP Framework](https://abp.io)** solution — modeled on ABP's own `Volo.Abp.Users`: contracts,
generics, and mechanism only, no field or host model of its own. Every downstream (a CMS, a commerce
catalog, …) owns its own field-definition entity, host entity, and database table; the kernel never
defines "the concrete one."

- **A self-built value bag, not `ExtraProperties`.** `IHasFlexFields.FlexFields` is FlexFields' own
  dictionary, isolated from ABP's shared `ExtraProperties` bag by design (see
  [`docs/flexfields-design.md`](docs/flexfields-design.md) §4).
- **One seam in, one seam out.** A downstream implements `IFlexFieldProvider<TEntity>` to tell the
  kernel what fields a host entity has; the kernel gives back validation
  (`IFlexFieldValidator<TEntity>`), a query pushdown (`IFlexFieldQueryExecutor<TEntity>`), derived-index
  maintenance (`IFlexFieldIndexManager<TEntity>`), and value-bag key migration for renamed or deleted
  field definitions (`IFlexFieldValueMigrator<TEntity>`).
- **Provider-agnostic by design.** The relational pivot-table shape (`FlexFieldIndexValue`,
  `EfCoreFlexFieldIndexManagerBase`, …) lives only in `Dignite.Abp.FlexFields.EntityFrameworkCore`;
  the MongoDB provider has no equivalent type and queries the value bag directly.

> **.NET 10 · ABP 10.5.0 · LGPL-3.0-only**

## Packages

| Package | Purpose |
|---|---|
| `Dignite.Abp.FlexFields.Domain.Shared` | Shared constants (`FlexFieldConsts`). Dependency-free. |
| `Dignite.Abp.FlexFields.Abstractions` | DDD-free contracts and vocabulary: `IFlexFieldData`/`FlexFieldData`, `IHasFlexFields`/`FlexFieldDictionary`, `FlexFieldValue`, `IFieldType` + the built-in field types (Text/Number/DateTime/Select/Boolean/Tree, plus the composite Matrix/Table and their `ICompositeFieldType`/`INormalizesValue`/`InlineFieldDefinition`/`CompositeFieldNesting` contracts), the query vocabulary, and the field-lifecycle Etos (`FlexFieldRenamedEto`, `FlexFieldDeletedEto`). Referencing this package alone is enough to implement a custom field type or type a downstream's DTOs. |
| `Dignite.Abp.FlexFields.Domain` | The Entity contract (`IFlexField : IAggregateRoot<Guid>`) and the DDD-aware seams: `IFlexFieldProvider<TEntity>`, `IFlexFieldValidator<TEntity>` (+ default impl), `IFlexFieldIndexManager<TEntity>`, `IFlexFieldQueryExecutor<TEntity>`, `IFlexFieldValueMigrator<TEntity>` (+ its one provider-agnostic default impl), `IFlexFieldRepository<TField>`. |
| `Dignite.Abp.FlexFields.EntityFrameworkCore` | EF Core support (not ownership): `ConfigureFlexFieldsProperty`/`ConfigureFlexField`/`ConfigureFlexFieldIndex` model-builder extensions, the typed pivot-row shape (`FlexFieldIndexValue`), and abstract base classes for the index manager, query executor, and field repository. Ships no `DbContext` and no table of its own. |
| `Dignite.Abp.FlexFields.MongoDB` | MongoDB support: queries and indexes the `FlexFieldDictionary` in place, so writes need almost no index synchronization. Deliberately has **no** counterpart to `FlexFieldIndexValue` — that shape is a relational pivot row. |
| `@dignite/ng.flex-fields` (npm) | Angular UI: config / control / view / search components for all eight field types, the `FieldTypeResolver` registry, and `provideFlexFields()`. See [`angular/projects/flex-fields`](./angular/projects/flex-fields/README.md). |

## Install

Add the layers a downstream project needs — a domain project typically needs `Domain` (which pulls in
`Domain.Shared` and `Abstractions` transitively), and an EF Core persistence project adds
`EntityFrameworkCore`:

```bash
dotnet add path/to/MyProject.Domain.csproj package Dignite.Abp.FlexFields.Domain --version 10.0.0-rc.4
dotnet add path/to/MyProject.EntityFrameworkCore.csproj package Dignite.Abp.FlexFields.EntityFrameworkCore --version 10.0.0-rc.4
```

A project that only needs the field-type vocabulary (for example, a shared DTO project) can reference
`Dignite.Abp.FlexFields.Abstractions` alone.

## Implementing the seam

A downstream owns its own field-definition entity, host entity, and `IFlexFieldProvider<TEntity>`:

```csharp
public class Field : AggregateRoot<Guid>, IFlexField { /* Name, DisplayName, FieldTypeName, ... */ }

public class Entry : AggregateRoot<Guid>, IHasFlexFields
{
    public virtual FlexFieldDictionary FlexFields { get; set; } = new();
}

public class EntryFlexFieldProvider : IFlexFieldProvider<Entry>
{
    // Merge this host's field definitions + per-usage Required/Searchable + entity.FlexFields
    // into FlexFieldValue instances - the kernel's only way to learn what fields Entry has.
}
```

The EF Core project maps the value bag, the field definition, and a per-host index table onto the
downstream's own `DbContext`:

```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);
    builder.Entity<Entry>(b => b.ConfigureFlexFieldsProperty<Entry>());
    builder.Entity<Field>(b => b.ConfigureFlexField<Field>());
    builder.Entity<EntryFlexFieldIndex>(b => b.ConfigureFlexFieldIndex<EntryFlexFieldIndex>());
}
```

See [`docs/flexfields-design.md`](docs/flexfields-design.md) for the full design rationale. For a
worked example, [`demo/`](demo/) is the same seam wired to a real, runnable feature
(`Product`/`ProductField`, an Angular admin UI, seeded data) — run it with
`dotnet run --project demo/Dignite.Abp.FlexFields.Demo -- --migrate-database` followed by
`dotnet run --project demo/Dignite.Abp.FlexFields.Demo` and `cd angular && npm start`. The
`Dignite.Abp.FlexFields.EntityFrameworkCore.Tests` project (`TestField`, `TestArticle`,
`TestArticleFlexFieldProvider`, …) is the same shape distilled to its minimum, without a UI.

## Renaming or deleting a field definition

A field definition's `Name` **is** the key its values are stored under in every host entity's bag, so
renaming or deleting one is a data migration, not a simple edit. `IFlexFieldValueMigrator<TEntity>` —
one provider-agnostic implementation, resolved for any host type with no downstream code required —
handles it:

```csharp
// after ruling out a duplicate with IFlexFieldRepository<TField>.NameExistsAsync(newName, excludedId)
// and changing the definition's own Name:
await migrator.RenameFieldAsync(oldName: "AuthorName", newName: "Author");
```

`FlexFieldRenamedEto` / `FlexFieldDeletedEto` in `.Abstractions` exist for downstreams whose field
definitions and host entities live in different modules; the kernel neither publishes nor handles
them. See the XML docs on `IFlexFieldValueMigrator<TEntity>` for the required ordering.

## Build & test

The library projects build through the repository's aggregate solution
(`Dignite.Abp.FlexFields.slnx` covers the demo host only):

```bash
dotnet build Dignite.Abp.Modules.slnx
dotnet test  flex-fields/test/Dignite.Abp.FlexFields.Tests
dotnet test  flex-fields/test/Dignite.Abp.FlexFields.EntityFrameworkCore.Tests
dotnet test  flex-fields/test/Dignite.Abp.FlexFields.MongoDB.Tests

# Pack for local testing (version / license come from the repository root Directory.Build.props)
dotnet pack Dignite.Abp.Modules.slnx -c Release
```

The Angular library is an npm workspace, outside MSBuild:

```bash
cd flex-fields/angular && npm install --legacy-peer-deps && npm run build:lib
```

`--legacy-peer-deps` is required: `@abp/ng.theme.shared` depends on `@swimlane/ngx-datatable`, whose
Angular peer range stops at 20, so npm will not hoist it beside Angular 21 without it.

Run the demo stack — the host on `https://localhost:44330`, the Angular app on `http://localhost:4200`:

```bash
dotnet run --project flex-fields/demo/Dignite.Abp.FlexFields.Demo
```

## Repository layout

```
src/Dignite.Abp.FlexFields.Domain.Shared        shared constants
src/Dignite.Abp.FlexFields.Abstractions         DDD-free contracts, field types, Etos
src/Dignite.Abp.FlexFields.Domain               Entity contract + DDD-aware seams
src/Dignite.Abp.FlexFields.EntityFrameworkCore  EF Core support (no DbContext, no table)
src/Dignite.Abp.FlexFields.MongoDB              MongoDB support (no pivot table)
test/                                           per-layer test projects
angular/projects/flex-fields                    publishable Angular library (@dignite/ng.flex-fields)
angular/src                                     Angular demo app - local dev only, never published
demo/                                           demo ABP host - local dev only, never packed
docs/flexfields-design.md                       design rationale
```

## License

Licensed under [LGPL-3.0-only](../LICENSE).
