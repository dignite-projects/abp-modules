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

## Field types

Six built-in types, each with up to four role components — **config** (design the field),
**control** (edit a value), **view** (display a value) and **search** (filter by it):

| Registration key | Type | Roles |
|---|---|---|
| `Text` | single- or multi-line text | config, control, view, search |
| `Number` | number with precision/step/bounds | config, control, view, search |
| `DateTime` | date, date-time or month | config, control, view |
| `Select` | single or multiple choice | config, control, view, search |
| `Boolean` | boolean | config, control, view, search |
| `Tree` | single or multiple selection from a node tree | config, control, view, search |

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
