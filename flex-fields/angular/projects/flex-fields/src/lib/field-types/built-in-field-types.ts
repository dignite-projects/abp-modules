import {
  DateTimeConfigComponent,
  DateTimeControlComponent,
  DateTimeViewComponent,
} from './date';
import { FieldTypeDefinition } from './field-type-definition';
import {
  NumberConfigComponent,
  NumberControlComponent,
  NumberSearchComponent,
  NumberViewComponent,
} from './number';
import {
  SelectConfigComponent,
  SelectControlComponent,
  SelectSearchComponent,
  SelectViewComponent,
} from './select';
import {
  BooleanConfigComponent,
  BooleanControlComponent,
  BooleanSearchComponent,
  BooleanViewComponent,
} from './boolean';
import {
  TextConfigComponent,
  TextControlComponent,
  TextSearchComponent,
  TextViewComponent,
} from './text';
import {
  TreeConfigComponent,
  TreeControlComponent,
  TreeSearchComponent,
  TreeViewComponent,
} from './tree';
import { MatrixConfigComponent, MatrixControlComponent, MatrixViewComponent } from './matrix';
import { TableConfigComponent, TableControlComponent, TableViewComponent } from './table';

/**
 * The field types this library ships. Each `name` is the value persisted in
 * `FlexFieldData.fieldTypeName` and must match its server counterpart's `Name` exactly:
 *
 * | key | server type | this library |
 * |---|---|---|
 * | `Text` | `TextFieldType` | `text/` |
 * | `Number` | `NumberFieldType` | `number/` |
 * | `DateTime` | `DateTimeFieldType` | `date/` |
 * | `Select` | `SelectFieldType` | `select/` |
 * | `Boolean` | `BooleanFieldType` | `boolean/` |
 * | `Tree` | `TreeFieldType` | `tree/` |
 * | `Matrix` | `MatrixFieldType` | `matrix/` |
 * | `Table` | `TableFieldType` | `table/` |
 *
 * A frozen array, not something to mutate at runtime: contributors register their own types through
 * the `FLEX_FIELD_TYPES` multi-provider instead.
 */
export const BUILT_IN_FIELD_TYPES: readonly FieldTypeDefinition[] = Object.freeze([
  {
    name: 'Text',
    displayNameKey: 'FlexFields::FieldType:Text',
    configComponent: TextConfigComponent,
    controlComponent: TextControlComponent,
    viewComponent: TextViewComponent,
    searchComponent: TextSearchComponent,
  },
  {
    name: 'Number',
    displayNameKey: 'FlexFields::FieldType:Number',
    configComponent: NumberConfigComponent,
    controlComponent: NumberControlComponent,
    viewComponent: NumberViewComponent,
    searchComponent: NumberSearchComponent,
  },
  {
    // No search component: the server indexes DateTime and allows six operators on it, but the old
    // library never shipped a date range filter and building one is feature work, not migration.
    name: 'DateTime',
    displayNameKey: 'FlexFields::FieldType:DateTime',
    configComponent: DateTimeConfigComponent,
    controlComponent: DateTimeControlComponent,
    viewComponent: DateTimeViewComponent,
  },
  {
    name: 'Select',
    displayNameKey: 'FlexFields::FieldType:Select',
    configComponent: SelectConfigComponent,
    controlComponent: SelectControlComponent,
    viewComponent: SelectViewComponent,
    searchComponent: SelectSearchComponent,
  },
  {
    name: 'Boolean',
    displayNameKey: 'FlexFields::FieldType:Boolean',
    configComponent: BooleanConfigComponent,
    controlComponent: BooleanControlComponent,
    viewComponent: BooleanViewComponent,
    searchComponent: BooleanSearchComponent,
  },
  {
    name: 'Tree',
    displayNameKey: 'FlexFields::FieldType:Tree',
    configComponent: TreeConfigComponent,
    controlComponent: TreeControlComponent,
    viewComponent: TreeViewComponent,
    searchComponent: TreeSearchComponent,
  },
  {
    // No search component: `MatrixFieldType.IndexValueType` is null on the server — the value is a
    // list of composite block objects, not something a filter control could meaningfully query.
    name: 'Matrix',
    displayNameKey: 'FlexFields::FieldType:Matrix',
    configComponent: MatrixConfigComponent,
    controlComponent: MatrixControlComponent,
    viewComponent: MatrixViewComponent,
    composite: true,
  },
  {
    // No search component: `TableFieldType.IndexValueType` is null on the server — the value is a
    // list of composite row objects, not something a filter control could meaningfully query.
    name: 'Table',
    displayNameKey: 'FlexFields::FieldType:Table',
    configComponent: TableConfigComponent,
    controlComponent: TableControlComponent,
    viewComponent: TableViewComponent,
    composite: true,
  },
]);
