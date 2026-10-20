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
  [`dev-docs/flexfields-design.md`](dev-docs/flexfields-design.md) §4).
- **One seam in, one seam out.** A downstream implements `IFlexFieldProvider<TEntity>` to tell the
  kernel what fields a host entity has; the kernel gives back validation
  (`IFlexFieldValidator<TEntity>`), a query pushdown (`IFlexFieldQueryExecutor<TEntity>`), derived-index
  maintenance (`IFlexFieldIndexManager<TEntity>`), and value-bag key migration for renamed or deleted
  field definitions (`IFlexFieldValueMigrator<TEntity>`).
- **Provider-agnostic by design.** The relational pivot-table shape (`FlexFieldIndexValue`,
  `EfCoreFlexFieldIndexManagerBase`, …) lives only in `Dignite.Abp.FlexFields.EntityFrameworkCore`;
  the MongoDB provider has no equivalent type and queries the value bag directly.

> **.NET 10 · ABP 10.7 · LGPL-3.0-only**

## Documentation

**[Read the documentation](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/flex-fields/index.md)**
— getting started with EF Core, the eight built-in field types and their configuration keys, creating a field type,
querying by field values, renaming and deleting fields, MongoDB, the Angular and MVC UIs, and the CKEditor field type.

## Packages

| Package | Purpose |
|---|---|
| `Dignite.Abp.FlexFields.Abstractions` | DDD-free contracts and vocabulary: the value bag, `FlexFieldValue`, `IFieldType` + the eight built-in field types (Text/Number/DateTime/Select/Boolean/Tree, plus the composite Matrix/Table), the query vocabulary, and the field-lifecycle Etos. |
| `Dignite.Abp.FlexFields.Domain.Shared` | Shared constants (`FlexFieldConsts`). |
| `Dignite.Abp.FlexFields.Domain` | The Entity contract (`IFlexField`) and the DDD-aware seams, with the default validator and value migrator. |
| `Dignite.Abp.FlexFields.EntityFrameworkCore` | Model-builder extensions, the typed pivot-row shape (`FlexFieldIndexValue`), and base classes for the index manager, query executor, and field repository. Ships no `DbContext` and no table. |
| `Dignite.Abp.FlexFields.MongoDB` | Queries and indexes the value bag in place; no counterpart to `FlexFieldIndexValue`. |
| `Dignite.Abp.FlexFields.Web` | `<flex-field-view>` / `<flex-field-search>` tag helpers for MVC / Razor Pages. |
| `Dignite.Abp.FlexFields.CKEditor` / `.CKEditor.Web` | Optional rich-text field type, and its MVC view. |
| `@dignite/ng.flex-fields` (npm) | Angular config / control / view / search components for every built-in field type. |
| `@dignite/ng.flex-fields-ckeditor` (npm) | Optional Angular half of the `CKEditor` field type. |

## Quick start

```bash
dotnet add path/to/MyProject.Domain.csproj package Dignite.Abp.FlexFields.Domain
dotnet add path/to/MyProject.EntityFrameworkCore.csproj package Dignite.Abp.FlexFields.EntityFrameworkCore
```

```csharp
public class ProductField : AggregateRoot<Guid>, IFlexField { /* Name, DisplayName, FieldTypeName, Configuration, ... */ }

public class Product : AggregateRoot<Guid>, IHasFlexFields
{
    public virtual FlexFieldDictionary FlexFields { get; set; } = new();
}

public class ProductFlexFieldProvider : IFlexFieldProvider<Product>, ITransientDependency
{
    // Merge this host's field definitions + per-usage Required/Searchable + entity.FlexFields
    // into FlexFieldValue instances - the kernel's only way to learn what fields Product has.
}
```

The documentation's [Getting Started](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/flex-fields/getting-started.md)
wires the database mapping, the index manager, the query executor and the save path.

## Development

```bash
dotnet build Dignite.Abp.FlexFields.slnx      # src/ + test/ + demo/
dotnet test  Dignite.Abp.FlexFields.slnx

dotnet run --project demo/Dignite.Abp.FlexFields.Demo -- --migrate-database   # first run only
dotnet run --project demo/Dignite.Abp.FlexFields.Demo                         # https://localhost:44330

cd angular && npm install --legacy-peer-deps && npm run build:lib && npm start   # http://localhost:4200
```

`demo/` wires the kernel to a real `Product` / `ProductField` feature with an Angular and an MVC UI and seeded data;
it is local-dev only and never packed. `--legacy-peer-deps` is required: `@abp/ng.theme.shared` 10.7.0 still depends
on `@swimlane/ngx-datatable` `~22.0.0`, whose Angular peer range stops at 20. The design rationale, including what was
rejected and why, is in [`dev-docs/flexfields-design.md`](dev-docs/flexfields-design.md).

## License

Licensed under [LGPL-3.0-only](https://github.com/dignite-projects/abp-modules/blob/main/LICENSE).
