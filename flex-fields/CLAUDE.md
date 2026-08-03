# Dignite.Abp.FlexFields

Runtime-defined ("flex") fields for ABP Framework (LGPL-3.0-only). A **constraint kernel** in the
mould of `Volo.Abp.Users`, not a runnable application module: it ships mechanism — field types,
configuration, validation, a value bag, a query index — and **no domain model**. Every downstream
(CMS today, Commerce later) owns its own field definitions and host entities.

Published: `src/` (six NuGet packages, plus `.Installer`), `angular/projects/flex-fields`
(`@dignite/ng.flex-fields`). `demo/` and the Angular demo app are local-dev-only, never packed.

Design rationale lives in [`docs/flexfields-design.md`](./docs/flexfields-design.md) — read §1–§5
before changing any contract; it records what was rejected and why.

## Structure

- **`src/`** — `Abstractions`, `Domain.Shared`, `Domain`, `EntityFrameworkCore`, `MongoDB`, `Web`,
  `Installer`. No `.Application` / `.HttpApi`: the kernel has no app service, so there is nothing to
  expose.
  - `.Web` is the one exception to "the three module trees never reference each other and flex-fields
    never reaches outside itself": it depends on `Dignite.Abp.AspNetCore.Mvc.Razor`, a separate
    top-level tree (`aspnetcore-mvc-razor/`) of domain-agnostic ASP.NET Core MVC/Razor infrastructure,
    not owned by any of the three modules — not a violation of that invariant, since it isn't one of
    "the three."
- **`demo/`** — single-project ABP host (`app-nolayers`, SQLite), in `Dignite.Abp.FlexFields.slnx`
  (this module's own focused solution, covering `src/` + `test/` + `demo/`) and in the aggregate
  `Dignite.Abp.Modules.slnx`, never packed: `dotnet run --project demo/Dignite.Abp.FlexFields.Demo`
  → `https://localhost:44330`. Wires the kernel to a real `Product`/`ProductField` feature — see
  "The demo" below.
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
| `FlexFields.Web` | `<flex-field-view>`/`<flex-field-search>` TagHelpers + default `.cshtml` per built-in type — SSR counterpart to the Angular library's `<ff-flex-field-view>`/`<ff-flex-field-search>`. No config/control TagHelpers | Abstractions, `Dignite.Abp.AspNetCore.Mvc.Razor` |
| `FlexFields.Installer` | ABP Studio/Suite install entry point, embeds the module's `.abpmdl` | `Volo.Abp.VirtualFileSystem` |

Bolt-on field types (optional, not part of the six above): `FlexFields.FileExplorer` (the field type
itself, references only Abstractions) and `FlexFields.FileExplorer.Web` (its `<flex-field-view>`
rendering — file name/size/MIME type/link, read straight out of the value the Angular picker already
denormalized at pick time; no search partial, since `FileExplorerFieldType.IndexValueType` is `null`).
Depending on `.FileExplorer.Web` alone pulls in `.FileExplorer` and `.Web` too. Each bolt-on's own
`.Web` counterpart is the pattern for any future one: a small project next to the field type itself,
depending on it plus `FlexFields.Web`, shipping one view at the same
`Views/Shared/FlexFields/{ControlName}.cshtml` convention path.

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
| `TreeView` | `TreeFieldType` | `tree/` |

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

## `.Web`: the SSR counterpart to the Angular library

`IFieldType`/`FieldTypeBase` carries **no rendering concerns on the C# side** by design (§3 of the
design doc: "命名领域不命名 UI" — name the domain, not the UI) — so `.Web`'s type-name → Razor-view
mapping lives entirely outside the kernel, the server-side mirror of how `FieldTypeDefinition`'s
`viewComponent`/`searchComponent` live in the Angular *library*, not on the core field type.

- `<flex-field-view field="@flexFieldValue">` / `<flex-field-search field="@flexFieldValue">` are
  zero-IO leaf renderers: they take an already-resolved `FlexFieldValue`, never a lookup key — the
  kernel has no application service to look one up with, so assembling it is the host's job, same as
  everywhere else in the kernel.
- Dispatch is by `FlexFieldValue.FieldTypeName` (the persisted registration key, e.g. `"TextEdit"`) to
  a conventional partial path (`Views/Shared/FlexFields/{Key}.cshtml`,
  `Views/Shared/FlexFields/Search/{Key}.cshtml`), resolved via `IRazorPartialRenderer` +
  `IRazorViewEngine`'s normal controller/`Shared`-relative search — a downstream overrides one built-in
  type, or adds a custom type, just by shipping a `.cshtml` at the same conventional path in its own
  project. `PartialName` on either TagHelper bypasses the convention entirely.
- `<flex-field-search>` only renders inputs. Turning what gets submitted into a
  `FlexFieldQueryCondition` stays the host's job — the Angular library can't do that translation either
  (see `ProductsComponent` below), so `.Web` doesn't try to; `ProductsWebController` in the demo does it
  for real, against the same `IFlexFieldQueryExecutor<Product>` the API layer uses.
- A referenced assembly's precompiled views are **not** reliably discovered through ABP's own
  `AddApplicationPartIfNotExists`/automatic module registration (both add a bare `AssemblyPart`, which
  `IViewsFeatureProvider` never surfaces views from) — every `.Web`-suffixed project's module registers
  itself through `Dignite.Abp.AspNetCore.Mvc.Razor`'s `AddCompiledRazorAssemblyPartIfNotExists`
  (the real `ApplicationPartFactory`) instead. Found by actually running the render pipeline in a test,
  not by compiling it; see `Dignite.Abp.FlexFields.Web.Tests`.
- The "downstream overrides one built-in type, or adds a custom type, at the same conventional path"
  claim two bullets up is not theoretical: `FlexFields.FileExplorer.Web` is exactly that, for the
  `FileExplorer` bolt-on (see the package table above) - a separate small project, not a fork of
  `.Web`, shipping one view at `Views/Shared/FlexFields/FileExplorer.cshtml`.

## The demo

`demo/Dignite.Abp.FlexFields.Demo` is the worked example of everything the previous section
describes, wired to a real feature instead of the test project's throwaway `TestArticle`/`TestField`:

- **`Entities/Product.cs`** (`IHasFlexFields`), **`ProductField.cs`** (`IFlexField` — `Required`/
  `Searchable` live directly on it, one table, not split into a separate usage entity the way the
  design doc's `EntryField` analogy would in a host with more than one flex-typed entity type — see
  the type's own doc comment for when that split becomes necessary), **`ProductFlexFieldIndex.cs`**
  (`FlexFieldIndexBase<Product>`).
- **`Services/FlexFields/`** — the four seam implementations: `ProductFlexFieldProvider`,
  `ProductFlexFieldIndexManager`, `ProductFlexFieldQueryExecutor`, `ProductFieldRepository`.
- **`Services/ProductFieldAppService.cs`** — field CRUD, demonstrating the ordering
  `IFlexFieldValueMigrator` documents: rename rewrites every product's bag *before* the definition's
  own `Name` changes; delete removes bag values *before* the definition; flipping `Searchable` calls
  `IFlexFieldIndexManager.RebuildAsync()`.
- **`Services/ProductAppService.cs`** — product CRUD plus `SearchAsync`, POST rather than the GET a
  `Get*`-prefixed name would default to. ABP's conventional controllers derive the URL from the
  *method name* convention, not from an `[HttpPost("...")]` attribute's route template string — a
  method still named `GetListAsync` collides on the same URL as `CreateAsync` no matter what
  attribute you add. The rename is why it's `SearchAsync`, at `POST /api/app/product/search`.
- **`Data/ProductDemoDataSeedContributor.cs`** — seeds one `ProductField` per built-in field type
  plus the FileExplorer bolt-on, and five products, so a first `dotnet run -- --migrate-database`
  leaves the demo immediately browsable instead of empty. One product's `images` field gets a real
  uploaded file (`FileDescriptorManager.CreateAsync` directly, bypassing the `[Authorize]`-gated app
  service the same way the field/product repositories are used directly elsewhere in this class) into
  the already-configured `"images"` container, not a fabricated value — so `FlexFields.FileExplorer.Web`
  has genuine data to render, and the other four products exercise the "no files" path.
- **`Controllers/ProductsWebController.cs`** + **`Views/ProductsWeb/Index.cshtml`** — the SSR
  counterpart to the Angular admin's products page, at `/ProductsWeb`: `<flex-field-view>` for the
  results table (`show-in-list`) and one full detail block, `<flex-field-search>` for the filter form.
  Also the one piece of translation `.Web` deliberately doesn't do: reads the submitted search inputs
  back by the exact names its own default search partials render (`{Name}`, `{Name}Min`/`Max`,
  `{Name}From`/`To`) and turns them into `FlexFieldQueryCondition`s for the same
  `IFlexFieldQueryExecutor<Product>` the API layer uses.
- **`angular/src/app/product-fields/`** and **`angular/src/app/products/`** — the Angular
  counterpart. `product-fields` is `<ff-flex-field-config>`'s demonstration site (swaps editors as
  the selected field type changes); `products` demonstrates all four host components at once:
  `<ff-flex-field-view>` for dynamic list columns, `<ff-flex-field-search>` for the filter bar,
  `<ff-flex-field-control>` for the create/edit form. `ProductsComponent` also owns the one piece of
  translation the library can't: turning each search control's raw value (a `"min-max"` string for
  `NumericEdit`, an array for `Select`/`TreeView`, the literal strings `"true"`/`"false"` for
  `Switch` — never a real boolean, because a native `<option [value]>` binding always stringifies)
  into `FlexFieldQueryCondition`s.

Both admin pages require the `Demo.Products`/`Demo.ProductFields` permission (grantable per role from
the Permission Management UI); the default admin account gets everything.

## Commands

```bash
dotnet build Dignite.Abp.FlexFields.slnx    # this module alone - src/ + test/ + demo/
dotnet test  Dignite.Abp.FlexFields.slnx

dotnet run --project demo/Dignite.Abp.FlexFields.Demo -- --migrate-database    # first run only
dotnet run --project demo/Dignite.Abp.FlexFields.Demo                          # :44330

cd angular && npx yarn && npx yarn build:lib && npx yarn start  # :4200
```

No migrations ship in the library projects — a consuming host owns its own DbContext and migrations.
