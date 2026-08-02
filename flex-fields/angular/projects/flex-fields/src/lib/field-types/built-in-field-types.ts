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
} from './numeric';
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
} from './switch';
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
} from './tree-view';

/**
 * The field types this library ships. Each `name` is the value persisted in
 * `FlexFieldData.fieldTypeName` and must match its server counterpart's `Name` exactly:
 *
 * | key | server type | this library |
 * |---|---|---|
 * | `TextEdit` | `TextFieldType` | `text/` |
 * | `NumericEdit` | `NumberFieldType` | `numeric/` |
 * | `DateEdit` | `DateTimeFieldType` | `date/` |
 * | `Select` | `SelectFieldType` | `select/` |
 * | `Switch` | `BooleanFieldType` | `switch/` |
 * | `TreeView` | `TreeFieldType` | `tree-view/` |
 *
 * A frozen array, not something to mutate at runtime: contributors register their own types through
 * the `FLEX_FIELD_TYPES` multi-provider instead.
 */
export const BUILT_IN_FIELD_TYPES: readonly FieldTypeDefinition[] = Object.freeze([
  {
    name: 'TextEdit',
    displayNameKey: 'FlexFields::FieldType:Text',
    indexable: true,
    configComponent: TextConfigComponent,
    controlComponent: TextControlComponent,
    viewComponent: TextViewComponent,
    searchComponent: TextSearchComponent,
  },
  {
    name: 'NumericEdit',
    displayNameKey: 'FlexFields::FieldType:Number',
    indexable: true,
    configComponent: NumberConfigComponent,
    controlComponent: NumberControlComponent,
    viewComponent: NumberViewComponent,
    searchComponent: NumberSearchComponent,
  },
  {
    // No search component: the server indexes DateTime and allows six operators on it, but the old
    // library never shipped a date range filter and building one is feature work, not migration.
    name: 'DateEdit',
    displayNameKey: 'FlexFields::FieldType:DateTime',
    indexable: true,
    configComponent: DateTimeConfigComponent,
    controlComponent: DateTimeControlComponent,
    viewComponent: DateTimeViewComponent,
  },
  {
    name: 'Select',
    displayNameKey: 'FlexFields::FieldType:Select',
    indexable: true,
    configComponent: SelectConfigComponent,
    controlComponent: SelectControlComponent,
    viewComponent: SelectViewComponent,
    searchComponent: SelectSearchComponent,
  },
  {
    name: 'Switch',
    displayNameKey: 'FlexFields::FieldType:Boolean',
    indexable: true,
    configComponent: BooleanConfigComponent,
    controlComponent: BooleanControlComponent,
    viewComponent: BooleanViewComponent,
    searchComponent: BooleanSearchComponent,
  },
  {
    name: 'TreeView',
    displayNameKey: 'FlexFields::FieldType:Tree',
    indexable: true,
    configComponent: TreeConfigComponent,
    controlComponent: TreeControlComponent,
    viewComponent: TreeViewComponent,
    searchComponent: TreeSearchComponent,
  },
]);
