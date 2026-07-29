# Dignite.Abp.FlexFields

Runtime-defined ("flex") fields for ABP Framework (LGPL-3.0-only). A **constraint kernel** in the
mould of `Volo.Abp.Users`, not a runnable application module: it ships mechanism — field types,
configuration, validation, a value bag, a query index — and **no domain model**. Every downstream
(CMS today, Commerce later) owns its own field definitions and host entities.

Published: `src/` (five NuGet packages), `angular/projects/flex-fields`
(`@dignite/ng.flex-fields`). `demo/` and the Angular demo app are local-dev-only, never packed.

Design rationale lives in [`docs/flexfields-design.md`](./docs/flexfields-design.md) — read §1–§5
before changing any contract; it records what was rejected and why.

## Structure

- **`src/`** — `Abstractions`, `Domain.Shared`, `Domain`, `EntityFrameworkCore`, `MongoDB`.
  No `.Application` / `.HttpApi`: the kernel has no app service, so there is nothing to expose.
- **`demo/`** — single-project ABP host (`app-nolayers`, SQLite), in the aggregate `.slnx`, never
  packed: `dotnet run --project demo/Dignite.Abp.FlexFields.Demo` → `https://localhost:44330`.
- **`angular/`** — publishable `flex-fields` library plus a demo app, npm-only, not in the `.slnx`,
  `http://localhost:4200`.

`.Abstractions` and `.Domain.Shared` do not reference each other; only `.Domain` depends on both.

| Project | Responsibility | Depends on |
|---|---|---|
| `FlexFields.Abstractions` | `IFieldType`/`FieldTypeBase` + the six built-ins, `IFlexFieldData`, `IHasFlexFields`, `FlexFieldValue`, query vocabulary, localization | ABP Core, Localization |
| `FlexFields.Domain.Shared` | `FlexFieldConsts` only | — |
| `FlexFields.Domain` | `IFlexField` (Entity contract), `IFlexFieldProvider<T>` and the other seams, provider-neutral `FlexFieldValidator`/`FlexFieldValueMigrator` | Abstractions, Domain.Shared, ABP DDD |
| `FlexFields.EntityFrameworkCore` | `FlexFieldIndexValue` (relational-only), index/repository base classes, model-creating extensions | Domain |
| `FlexFields.MongoDB` | Embedded values, native path indexes — deliberately **no** pivot-table type | Domain |

## Two hard invariants

1. **`.Abstractions` contains no Entity contract.** `IFlexField` and `IFlexFieldRepository` belong to
   `.Domain`. Etos are the documented exception (precedent: `Volo.Abp.Users.Abstractions`'s `UserEto`).
2. **`FlexFieldIndexValue` is EF-only.** It is the shape of a relational pivot row. MongoDB queries
   the `FlexFieldDictionary` directly, which is the whole reason the two providers are separate.

## Type names are code; registration keys and configuration keys are data

The C# rename (`IFormControl` → `IFieldType`, `TextEditFormControl` → `TextFieldType`, …) **did not**
move the persisted strings. `TextFieldType.ControlName` is still `"TextEdit"`, and `NumericConfiguration`
still writes `NumericEditField.Decimals` (not `NumericEdit.`) alongside an unprefixed `FormatSpecifier`.

| Registration key | C# type | Angular folder |
|---|---|---|
| `TextEdit` | `TextFieldType` | `text/` |
| `NumericEdit` | `NumberFieldType` | `numeric/` |
| `DateEdit` | `DateTimeFieldType` | `date/` |
| `Select` | `SelectFieldType` | `select/` |
| `Switch` | `BooleanFieldType` | `switch/` |
| `TreeView` | `TreeFieldType` | `tree-view/` |

Renaming any of these "for consistency" orphans every field already stored under the old key, and
nothing in the build catches it. `built-in-field-types.spec.ts` asserts all of them for that reason.

## The seams

The kernel's only information entry point is `IFlexFieldProvider<TEntity>` — a downstream merges its
field definitions (`IFlexField` → `ToFlexFieldData()`), its per-usage `Required`/`Searchable` flags,
and the value from the host's bag into `FlexFieldValue`. Keep those three parts separate: the same
definition attaches to several host types with different rules.

Adding a field type means implementing `FieldTypeBase` (auto-registered via `ITransientDependency`)
and, on the Angular side, registering a `FieldTypeDefinition` through `provideFlexFieldTypes()`.
There is no options/registry class to add to.

## Commands

```bash
dotnet build Dignite.Abp.Modules.slnx
dotnet test  Dignite.Abp.Modules.slnx

cd angular && npm install --legacy-peer-deps && npm run build:lib && npm start  # :4200
dotnet run --project demo/Dignite.Abp.FlexFields.Demo                          # :44330
```

`--legacy-peer-deps` is required: `@abp/ng.theme.shared` pulls in `@swimlane/ngx-datatable`, whose
Angular peer range stops at 20, so npm will not hoist it beside Angular 21 without it.

No migrations ship in the library projects — a consuming host owns its own DbContext and migrations.
