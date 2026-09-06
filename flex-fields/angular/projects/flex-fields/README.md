# @dignite/ng.flex-fields

Angular UI for the **[Dignite.Abp.FlexFields](https://github.com/dignite-projects/abp-modules/tree/main/flex-fields)**
ABP module — render, configure, display and filter runtime-defined ("flex") fields.

> **Angular 21 · ABP 10.5 · LGPL-3.0-only**

FlexFields is a *constraint kernel*: it supplies the mechanism (field types, configuration,
validation, indexing) and never owns the domain model. This package is its front-end half. It
provides the rendering and registry axis that the C# side deliberately has no counterpart for —
`IFieldType` on the server has no rendering member at all.

## Install

```bash
npm install @dignite/ng.flex-fields
```

### Peer dependencies

`@abp/ng.components` is a **dependency** of this package, not a peer — you do not declare it, and it
is installed for you. It is deliberately not a peer: an ABP Angular host is not guaranteed to have it
(neither `@abp/ng.core` nor `@abp/ng.theme.shared` depends on it — only feature packages such as
`@abp/ng.identity` do), so asking the consumer for it left this package's `@abp/ng.components/tree`
import resolving to nothing on any install that skips peers, `--legacy-peer-deps` included.

Everything in `peerDependencies` you **must** declare yourself. Two are easy to miss because no stock
ABP host brings them in: `ng-zorro-antd` (`^21.0.0`) and `@angular/cdk` (`~21.2.0`), both imported
directly by the field controls. Note that `@abp/ng.components` pins `ng-zorro-antd` at
`~21.0.0-next.1`, i.e. `<21.1.0`. Declaring a wider range — `^21.0.2`, or the `21.3.3` current hosts
run — is supported and expected, but no single version satisfies both, so your package manager
installs two: yours at the root and `21.0.2` nested under `@abp/ng.components`. Two copies are two
module-scoped `NZ_CONFIG` / `NzConfigService` injection tokens, so `provideNzConfig()` and
`provideNzI18n()` configure the copy these controls use and not the one ABP's `abp-tree` sees. Pin
inside `<21.1.0` if you need a single copy; otherwise expect `abp-tree` to run on ng-zorro defaults.

### Styles

ng-zorro-antd ships no component styles: every component is `ViewEncapsulation.None` and its CSS is a
separate opt-in the application loads. Two of its stylesheets are therefore **served by your host
under a fixed name** and fetched at runtime by the components that need them. Declare both in the
`styles` array of your `angular.json` build target:

```json
{ "input": "node_modules/ng-zorro-antd/select/style/index.min.css", "inject": false, "bundleName": "ng-zorro-antd-select" },
{ "input": "node_modules/ng-zorro-antd/tree/style/index.min.css", "inject": false, "bundleName": "ng-zorro-antd-tree" }
```

`ng-zorro-antd-select` is loaded by the `Select` field type's control and search components, which
render `<nz-select>`. `ng-zorro-antd-tree` is loaded by `<abp-tree>` from `@abp/ng.components`, which
the `Tree` field types render — that entry is the contract ABP's own Tree component docs prescribe,
and this package neither adds to it nor overrides it. Nothing else needs declaring: the file names
above are the only `ng-zorro-antd` CSS either package asks for.

`inject: false` is not a preference. `bundleName` is the name asked for at runtime
(`<bundleName>.css`), and under the production `outputHashing: "all"` an **injected** entry is
emitted under a content hash — this repo's demo build produces `fontawesome-all.min-GM54M4UG.css`,
not `fontawesome-all.min.css` — which no fixed name can find. A non-injected entry keeps its literal
file name and stays out of `index.html`, which is exactly what a bundle fetched by name needs.

If an entry is missing, the control still works — unstyled — and the browser console carries one
error naming the file and quoting the entry to add:

```text
[@dignite/ng.flex-fields] Could not load "ng-zorro-antd-select.css". This stylesheet is not compiled
into the package: the host serves its own copy of it under a fixed bundle name. Add this entry to the
"styles" array of your angular.json build target and rebuild: …
```

An application that already has that CSS some other way — its own `.less` build, an `inject: true`
entry, a CDN `<link>` in `index.html` — should switch the loading off rather than fetch it twice:

```ts
providers: [
  { provide: DISABLE_FLEX_FIELDS_STYLE_LOADING_TOKEN, useValue: true }, // @dignite/ng.flex-fields*
  { provide: DISABLE_TREE_STYLE_LOADING_TOKEN, useValue: true },        // @abp/ng.components/tree
]
```

The two are independent. `DISABLE_FLEX_FIELDS_STYLE_LOADING_TOKEN` is family-wide and all-or-nothing:
`true` silences every bundle this package **and its sibling packages** load, so an application that
wants one of them and not the others declares the entries it wants and leaves the token unset.
`DISABLE_TREE_STYLE_LOADING_TOKEN` is ABP's own and covers only `abp-tree`.

`ng-zorro-antd/style/index.min.css` — ant-design's global reset (`body`/`html`/`h1`-`h6`/`a`/`button`,
etc.) — is neither required nor recommended in a Bootstrap/LeptonX host; this package never loads it
and your app should not either.

#### Sibling packages

The bolt-on packages declare their own bundles the same way, through the same loader and the same
token, each documented in its own README:

- [`@dignite/ng.flex-fields-ckeditor`](https://github.com/dignite-projects/abp-modules/blob/main/flex-fields/angular/projects/flex-fields-ckeditor/README.md#styles) — one entry, for
  CKEditor 5's UI stylesheet.
- [`@dignite/ng.flex-fields-file-explorer`](https://github.com/dignite-projects/abp-modules/blob/main/flex-fields/angular/projects/flex-fields-file-explorer/README.md#styles) — no entry
  of its own.

**Maintainers:** the `StyleBundle` constant lives in the package whose component renders the
CSS, not centrally — `NZ_SELECT_STYLE` here (`src/lib/utils/style-loader.service.ts`), `CKEDITOR5_STYLE`
in the CKEditor package. A package that starts rendering a component with its own global CSS defines
the constant there, calls `FlexFieldsStyleLoader.load()` from that component's `ngOnInit`, and updates
its own README "Styles" section and the changelog in the same PR.

## Field types

Eight built-in types, each with up to four role components — **config** (design the field),
**control** (edit a value), **view** (display a value) and **search** (filter by it):

| Registration key | Type | Roles |
|---|---|---|
| `Text` | single- or multi-line text | config, control, view, search |
| `Number` | number with precision/step/bounds | config, control, view, search |
| `DateTime` | date, date-time or month | config, control, view |
| `Select` | single or multiple choice | config, control, view, search |
| `Boolean` | boolean | config, control, view, search |
| `Tree` | single or multiple selection from a node tree | config, control, view, search |
| `Matrix` | repeatable list of polymorphic blocks, each block type with its own sub-fields | config, control, view |
| `Table` | repeatable grid, one shared column schema for every row | config, control, view |

`Matrix` and `Table` are **composite**: their configuration declares further fields, and their config,
control and view components recurse through `<ff-flex-field-config>` / `<ff-flex-field-control>` /
`<ff-flex-field-view>` to render them. They ship no search component — the server's
`IndexValueType` is `null` for both, so there is nothing to filter on. How deep the recursion may go
is the server's `CompositeFieldNesting.MaxDepth`; the config editors mirror it only so they can stop
offering composite types once the limit is reached.

The registration keys are the values persisted in `IFlexFieldData.FieldTypeName` on the server.
They are **data, not class names** — `Text` is served by `TextFieldType` in C# and
`TextControlComponent` here. Configuration dictionary keys (`Text.CharLimit`,
`Number.Decimals`, …) are likewise stored values and match the server byte-for-byte.

## Usage

Register the built-in field types once, in your application config:

```ts
import { provideFlexFields } from '@dignite/ng.flex-fields';

export const appConfig: ApplicationConfig = {
  providers: [provideFlexFields()],
};
```

Add your own field type — or one from a bolt-on package — by passing its definition:

```ts
provideFlexFields({
  name: 'CkEditor',
  displayNameKey: 'MyApp::FieldType:RichText',
  configComponent: CkEditorConfigComponent,
  controlComponent: CkEditorControlComponent,
  viewComponent: CkEditorViewComponent,
});
```

Then render a field by role:

```html
<ff-flex-field-control [fields]="fieldValue" [entity]="form" />
<ff-flex-field-view [type]="field.fieldTypeName" [value]="value" />
```

## Localization

All strings resolve through the ABP `FlexFields` localization resource, which ships inside
`Dignite.Abp.FlexFields.Abstractions`. Your host must depend on `FlexFieldsAbstractionsModule` for
them to reach the browser via the application-configuration endpoint.

## License

LGPL-3.0-only. See the [repository](https://github.com/dignite-projects/abp-modules).
