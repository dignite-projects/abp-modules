import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges } from '@angular/core';
import { FlexFieldViewComponent } from '../../components/flex-field-view.component';
import type { FlexFieldValue } from '../../models';
import type { InlineFieldDefinition } from '../inline-field-definition';
import { normalizeInlineFieldDefinitions } from '../inline-field-definition';
import type { TableRow } from './table-row';
import { normalizeTableRows } from './table-row';

/**
 * Displays the value of a `Table` field read-only, as a literal table: one column per configured
 * `InlineFieldDefinition`, each cell recursively rendered via `<ff-flex-field-view>` in list mode -
 * reusing the existing top-level dispatcher rather than hand-writing rendering per column field type.
 */
@Component({
  selector: 'ff-table-view',
  templateUrl: './table-view.component.html',
  imports: [CommonModule, FlexFieldViewComponent],
})
export class TableViewComponent implements OnChanges {
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type - always `Table` here. */
  @Input() type?: string;

  @Input() value: unknown = '';

  rows: TableRow[] = [];
  columns: InlineFieldDefinition[] = [];

  /** `columnValueOf` is called from the template on every change-detection cycle, and
   * `<ff-flex-field-view>` only keeps its mounted child alive across a cycle if `[fields]` is
   * reference-equal to what it rendered last time - a fresh object literal per call defeats that and
   * tears down/rebuilds every recursively-mounted view on every cycle, not just when the value actually
   * changes. Keyed on `row` (outer) then column name (inner): `ngOnChanges` replaces `this.rows`
   * wholesale on a real value change, so stale entries for old row objects simply stop being reachable
   * rather than needing explicit invalidation. */
  private readonly columnValueCache = new WeakMap<TableRow, Map<string, FlexFieldValue>>();

  ngOnChanges(): void {
    this.rows = normalizeTableRows(this.value);
    this.columns = normalizeInlineFieldDefinitions(this.fields?.field.configuration['Table.Columns']);
  }

  columnValueOf(column: InlineFieldDefinition, row: TableRow): FlexFieldValue {
    let rowCache = this.columnValueCache.get(row);
    if (!rowCache) {
      rowCache = new Map();
      this.columnValueCache.set(row, rowCache);
    }

    let value = rowCache.get(column.name);
    if (!value) {
      value = {
        field: {
          id: '',
          name: column.name,
          displayName: column.displayName,
          description: column.description,
          fieldTypeName: column.fieldTypeName,
          configuration: column.configuration,
        },
        required: column.required,
        searchable: false,
        value: row.values[column.name],
      };
      rowCache.set(column.name, value);
    }
    return value;
  }
}
