import { FormArray } from '@angular/forms';

/**
 * Configuration of a `Table` field, shaped for `FormBuilder.group()`. Mirrors `TableConfiguration`
 * (`src/Dignite.Abp.FlexFields.Abstractions/Dignite/Abp/FlexFields/Table/TableConfiguration.cs`).
 */
export class TableConfiguration {
  'Table.Columns': unknown = new FormArray<never>([]);
}
