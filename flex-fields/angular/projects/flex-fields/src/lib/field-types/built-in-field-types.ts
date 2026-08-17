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
]);
