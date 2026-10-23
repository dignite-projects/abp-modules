```json
//[doc-seo]
{
    "Description": "The Angular UI of FlexFields: components to configure fields, edit, display and filter their values for every built-in field type, and registering field types of your own."
}
```

# Angular UI

The `@dignite/ng.flex-fields` npm package is the front-end half of FlexFields. Every built-in field type comes with up to four **role** components:

* **config**: Designs a field: the editor of its configuration, in a field designer.
* **control**: Edits a value, in an entity's form.
* **view**: Displays a value, in a list cell or a detail page.
* **search**: Filters by a value, in a filter bar.

Four host components render the right role component for a field's type, so your pages never switch on field types themselves.

| Field type | config | control | view | search |
|---|---|---|---|---|
| `Text` | ✓ | ✓ | ✓ | ✓ |
| `Number` | ✓ | ✓ | ✓ | ✓ |
| `DateTime` | ✓ | ✓ | ✓ | |
| `Select` | ✓ | ✓ | ✓ | ✓ |
| `Boolean` | ✓ | ✓ | ✓ | ✓ |
| `Tree` | ✓ | ✓ | ✓ | ✓ |
| `Matrix` | ✓ | ✓ | ✓ | |
| `Table` | ✓ | ✓ | ✓ | |

`Matrix` and `Table` render their inline fields through the same host components, recursively.

## Installation

````bash
npm install @dignite/ng.flex-fields
````

Use the same version as the NuGet packages of the server. Besides the peer dependencies an ABP Angular application already has, declare two that no stock ABP application brings: `ng-zorro-antd` (`~21.3.1`) and `@angular/cdk` (`~21.2`). Keep them in these ranges, the ones ABP 10.7's `@abp/ng.components` uses, so there is a single copy of each.

Then register the built-in field types in `app.config.ts`:

````ts
import { provideFlexFields } from '@dignite/ng.flex-fields';

export const appConfig: ApplicationConfig = {
  providers: [
    // ...ABP providers
    provideFlexFields(),
  ],
};
````

The texts of the components come from the `FlexFields` localization resource of the server, so the application must depend on `AbpFlexFieldsAbstractionsModule` for them to reach the browser.

### Styles

ng-zorro-antd ships no component styles, so the `Select` and `Tree` components load two stylesheets at runtime, by a fixed name, which your application serves. Add both to the `styles` of your `angular.json` build target:

````json
{ "input": "node_modules/ng-zorro-antd/select/style/index.min.css", "inject": false, "bundleName": "ng-zorro-antd-select" },
{ "input": "node_modules/ng-zorro-antd/tree/style/index.min.css", "inject": false, "bundleName": "ng-zorro-antd-tree" }
````

`inject: false` is required: an injected entry is emitted under a content hash in a production build, which a fixed name cannot find. Without an entry, the control still works, unstyled, and the browser console names the entry to add. An application that already loads this CSS another way switches the loading off with the `DISABLE_FLEX_FIELDS_STYLE_LOADING_TOKEN` (and ABP's own `DISABLE_TREE_STYLE_LOADING_TOKEN` for `abp-tree`).

Do not load ng-zorro-antd's global stylesheet (`ng-zorro-antd/style/index.min.css`) in a Bootstrap or LeptonX application; the components do not need it.

## Rendering Fields

The host components take a `FlexFieldValue` (the field definition, the usage flags and the value), in the shape of the server's `FlexFieldValue`:

````ts
const fieldValue: FlexFieldValue = {
  field: { id, name, displayName, description, fieldTypeName, configuration },
  required: true,
  searchable: false,
  value: product.flexFields[name],
};
````

### Designing a Field

`<ff-flex-field-config>` renders the configuration editor of a field type into the `configuration` group of your field-definition form. It swaps the editor when the selected field type changes:

````html
<form [formGroup]="form">
  <input formControlName="name" />
  <select formControlName="fieldTypeName">
    @for (type of fieldTypes; track type.name) {
      <option [value]="type.name">{%{{{ type.displayNameKey | abpLocalization }}}%}</option>
    }
  </select>

  <ff-flex-field-config
    [type]="form.get('fieldTypeName')?.value"
    [selected]="editingField"
    [form]="form"
  ></ff-flex-field-config>
</form>
````

`FieldTypeResolver.getAll()` returns the registered field types, to offer them to choose from.

### Editing Values

`<ff-flex-field-control>` adds a control for the field to a group of your entity's form, named by `parentFieldName`, and seeds it with `selected`:

````html
<form [formGroup]="form">
  <input formControlName="name" />

  @for (field of fields; track field.id) {
    <ff-flex-field-control
      [fields]="valueOf(field)"
      [entity]="form"
      parentFieldName="flexFields"
      [selected]="product?.flexFields[field.name]"
    ></ff-flex-field-control>
  }
</form>
````

````ts
this.form = this.fb.group({
  name: ['', Validators.required],
  flexFields: this.fb.group({}),
});
````

`form.get('flexFields').getRawValue()` is then the value bag to send to the server. The controls apply the field's `required` flag and its configuration as Angular validators; the server validates again.

### Displaying Values

````html
<ff-flex-field-view
  [type]="field.fieldTypeName"
  [value]="product.flexFields[field.name]"
  [fields]="valueOf(field)"
  [showInList]="true"
></ff-flex-field-view>
````

Set `showInList` in a list cell, for a compact rendering.

### Search

`<ff-flex-field-search>` adds the filter inputs of a searchable field to a group of your filter form, in the same way as the control component. It renders nothing for a field type without a search component.

Turning what was entered into [query conditions](querying.md#query-conditions) is your application's job, because it depends on the field type. The built-in search components write these values:

| Field type | Value | Condition |
|---|---|---|
| `Text` | The text | `Contains`, `String` |
| `Number` | A `"min-max"` string | `GreaterThanOrEqual` and `LessThanOrEqual`, `Number` |
| `Select`, `Tree` | The selected values | `In` over the values joined by commas, `String` |
| `Boolean` | The string `"true"` or `"false"` | `Equals`, `Boolean` |

## Adding a Field Type

Register a field type of your own, or one of a bolt-on package such as the [CKEditor field type](ckeditor.md), with its components:

````ts
provideFlexFields({
  name: 'MyProject.Color',
  displayNameKey: 'MyProject::FieldType:Color',
  configComponent: ColorConfigComponent,
  controlComponent: ColorControlComponent,
  viewComponent: ColorViewComponent,
  searchComponent: ColorSearchComponent,
});
````

`provideFlexFieldTypes(...)` registers field types without the built-in ones, for a library that adds its own. The `name` must match the server's field type name. A later registration under the same name overrides an earlier one, which is also how you replace a built-in type's components.

The role components are standalone components. Derive a control or search component from `FieldTypeControlBase`, which binds the field to its named control inside the value-bag group and removes it again on destroy; implement `createControl()` and `configurationDefaults()`. Derive a config component from `FieldTypeConfigBase`, which swaps a `configuration` group of your field type's shape into the field-definition form and loads the stored values. The built-in components in the package's `field-types` folder are the examples to follow.

## See Also

* [MVC UI](mvc-ui.md)
* [Querying by Field Values](querying.md)
