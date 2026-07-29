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

## Field types

Six built-in types, each with up to four role components — **config** (design the field),
**control** (edit a value), **view** (display a value) and **search** (filter by it):

| Registration key | Type | Roles |
|---|---|---|
| `TextEdit` | single- or multi-line text | config, control, view, search |
| `NumericEdit` | number with precision/step/bounds | config, control, view, search |
| `DateEdit` | date, date-time or month | config, control, view |
| `Select` | single or multiple choice | config, control, view, search |
| `Switch` | boolean | config, control, view, search |
| `TreeView` | single or multiple selection from a node tree | config, control, view, search |

The registration keys are the values persisted in `IFlexFieldData.FieldTypeName` on the server.
They are **data, not class names** — `TextEdit` is served by `TextFieldType` in C# and
`TextControlComponent` here. Configuration dictionary keys (`TextEdit.CharLimit`,
`NumericEditField.Decimals`, …) are likewise stored values and match the server byte-for-byte.

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
