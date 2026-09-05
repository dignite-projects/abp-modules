import { CoreModule } from '@abp/ng.core';
import { Component, inject } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FlexFieldConfigComponent } from '../../components/flex-field-config.component';
import type { FlexFieldData } from '../../models';
import {
  COMPOSITE_NESTING_DEPTH,
  allowsCompositeAt,
  nextCompositeNestingDepth,
} from '../composite-nesting';
import { FieldTypeConfigBase } from '../field-type-config-base';
import type { FieldTypeDefinition } from '../field-type-definition';
import { FieldTypeResolver } from '../field-type-resolver.service';
import type { InlineFieldDefinition } from '../inline-field-definition';
import { normalizeInlineFieldDefinitions } from '../inline-field-definition';
import { TableConfiguration } from './table-configuration';

let nextInstanceId = 0;

/**
 * Designer-side editor for a `Table` field's configuration: the one shared column schema. Simpler than
 * `MatrixConfigComponent` - a single flat list instead of block types each with their own list - but
 * built the same way: each column's own type-specific configuration is delegated to a
 * recursively-mounted `<ff-flex-field-config>`.
 *
 * Master-detail instead of a stacked list: the columns list on the left selects which column's own
 * editor renders on the right, so opening a column with a nested type (its own `ff-flex-field-config`,
 * e.g. Select's option list) doesn't push every other column down the page.
 */
@Component({
  selector: 'ff-table-config',
  templateUrl: './table-config.component.html',
  imports: [CoreModule, ReactiveFormsModule, FlexFieldConfigComponent],
  providers: [{ provide: COMPOSITE_NESTING_DEPTH, useFactory: nextCompositeNestingDepth }],
})
export class TableConfigComponent extends FieldTypeConfigBase {
  private readonly fieldTypeResolver = inject(FieldTypeResolver);

  /** The depth this table's own columns live at - 2 for a top-level Table field. */
  private readonly columnDepth = inject(COMPOSITE_NESTING_DEPTH);

  /**
   * Which registered field types are themselves composite, read straight off the registry - the
   * client-side counterpart of the server's `ICompositeFieldType`, declared by whichever package ships
   * each type (see `FieldTypeDefinition.composite`). Read once: registrations are fixed for the
   * lifetime of the injector.
   */
  private readonly compositeFieldTypeNames: ReadonlySet<string> = new Set(
    this.fieldTypeResolver
      .getAll()
      .filter(fieldType => fieldType.composite)
      .map(fieldType => fieldType.name),
  );

  /** Seed value for each column's own recursively-mounted config editor, keyed by its FormGroup - see
   * `MatrixConfigComponent`'s identical field for why this can't just live on the FormGroup itself.
   *
   * Re-captured from the live form whenever a column is deselected ({@link captureSeed}): only the
   * selected column's `<ff-flex-field-config>` is mounted, and `FieldTypeConfigBase` re-seeds the
   * `configuration` group from this map every time it mounts - so a stale seed here silently reverts
   * whatever the admin just typed into that column's nested editor. */
  private readonly columnSeeds = new WeakMap<FormGroup, FlexFieldData | undefined>();

  /** Distinguishes this instance's DOM ids from another Table config's - a Table column can be a Matrix
   * whose own sub-field is a Table, so two of these can be on the page at once, and a plain
   * `table-config-required-0` on both would make the inner checkbox's label toggle the outer one. */
  readonly instanceId = `table-config-${nextInstanceId++}`;

  /** Which column's detail is shown on the right - null only when the table has no columns at all. */
  selectedColumnIndex: number | null = null;

  /**
   * The types a column may be bound to. Composite types drop out once there is no room left under a
   * column for the fields *they* would declare - so at the current `MAX_COMPOSITE_NESTING_DEPTH` of 3,
   * a top-level Table's columns may still be composite, but *their* own sub-fields may not.
   *
   * Note there is no "a Table column cannot be a Table" special case: self-nesting was never the thing
   * worth blocking (`Table > Matrix > Table` sidestepped it and reached exactly the same shape), so
   * depth is the single rule now, applied to every composite type alike.
   *
   * Whatever the selected column is *already* bound to stays in the list even when the rule would drop
   * it, so a configuration stored before the limit existed still shows what it is rather than an empty
   * select. Saving it still fails - `CompositeFieldNesting` is the constraint - which is the honest
   * outcome.
   */
  get fieldTypeOptions(): readonly FieldTypeDefinition[] {
    if (allowsCompositeAt(this.columnDepth)) {
      return this.fieldTypeResolver.getAll();
    }

    const boundFieldTypeName = this.selectedColumn ? this.fieldTypeNameOf(this.selectedColumn) : '';

    return this.fieldTypeResolver
      .getAll()
      .filter(
        fieldType =>
          !this.compositeFieldTypeNames.has(fieldType.name) || fieldType.name === boundFieldTypeName,
      );
  }

  get columns(): FormArray<FormGroup> {
    return this.configuration.controls['Table.Columns'] as FormArray<FormGroup>;
  }

  get selectedColumn(): FormGroup | undefined {
    return this.selectedColumnIndex !== null ? this.columns.at(this.selectedColumnIndex) : undefined;
  }

  protected configurationDefaults(): object {
    return new TableConfiguration();
  }

  protected override onConfigurationPatched(): void {
    const stored = normalizeInlineFieldDefinitions(this.selectedField?.configuration['Table.Columns']);
    stored.forEach(column => this.addColumn(column));
    this.selectedColumnIndex = this.columns.length > 0 ? 0 : null;
  }

  protected override onConfigurationReset(): void {
    // A field being created starts with one blank column, the same "seed one row" convention
    // `SelectConfigComponent` uses for its own option list.
    this.addColumn();
  }

  addColumn(seed?: InlineFieldDefinition): FormGroup {
    const group = new FormGroup({
      name: new FormControl(seed?.name ?? '', Validators.required),
      displayName: new FormControl(seed?.displayName ?? '', Validators.required),
      description: new FormControl(seed?.description ?? ''),
      fieldTypeName: new FormControl(seed?.fieldTypeName ?? '', Validators.required),
      required: new FormControl(seed?.required ?? false),
      // Carries the stored configuration for a column the admin never opens. Only the selected column
      // mounts an `<ff-flex-field-config>`, and that is what would otherwise add this control - so
      // without seeding it here, saving after editing anything else drops every unopened column's own
      // configuration. Replaced wholesale by `FieldTypeConfigBase` the moment the column is selected.
      configuration: new FormControl(seed?.configuration ?? {}),
    });

    this.columnSeeds.set(
      group,
      seed
        ? {
            id: '',
            name: seed.name,
            displayName: seed.displayName,
            description: seed.description,
            fieldTypeName: seed.fieldTypeName,
            configuration: seed.configuration,
          }
        : undefined,
    );

    // Adding deselects whatever was open, so its live configuration has to be snapshotted first.
    this.captureSeed(this.selectedColumnIndex);
    this.columns.push(group);
    this.selectedColumnIndex = this.columns.length - 1;
    return group;
  }

  removeColumn(index: number): void {
    this.columns.removeAt(index);

    if (this.columns.length === 0) {
      this.selectedColumnIndex = null;
    } else if (this.selectedColumnIndex !== null) {
      if (index < this.selectedColumnIndex) {
        this.selectedColumnIndex -= 1;
      } else if (index === this.selectedColumnIndex) {
        this.selectedColumnIndex = Math.min(index, this.columns.length - 1);
      }
    }
  }

  selectColumn(index: number): void {
    this.captureSeed(this.selectedColumnIndex);
    this.selectedColumnIndex = index;
  }

  /**
   * Snapshots a column's *current* nested configuration into {@link columnSeeds}, so that re-selecting
   * it re-seeds its editor from what the admin last had on screen rather than from what was loaded off
   * the server.
   *
   * Deliberately writes a new object only here, on deselection - never per change-detection cycle.
   * `columnSeedOf` feeds `<ff-flex-field-config>`'s `[selected]` input, and a fresh object literal on
   * every cycle would make that input look changed every cycle, tearing the nested editor down and
   * rebuilding it mid-keystroke (the same reference-stability trap `TableControlComponent`'s
   * `columnValueCache` exists for).
   */
  private captureSeed(index: number | null): void {
    if (index === null) {
      return;
    }

    const column = this.columns.at(index);
    const configuration = column?.get('configuration')?.value as Record<string, unknown> | undefined;
    if (!column || !configuration) {
      return;
    }

    this.columnSeeds.set(column, {
      id: '',
      name: column.get('name')?.value ?? '',
      displayName: column.get('displayName')?.value ?? '',
      description: column.get('description')?.value ?? undefined,
      fieldTypeName: this.fieldTypeNameOf(column),
      configuration,
    });
  }

  fieldTypeNameOf(column: FormGroup): string {
    return column.get('fieldTypeName')?.value ?? '';
  }

  /** Localization key for a column's field type, for the left-hand list's subtitle. */
  fieldTypeDisplayNameKeyOf(column: FormGroup): string {
    const fieldTypeName = this.fieldTypeNameOf(column);
    return this.fieldTypeResolver.find(fieldTypeName)?.displayNameKey ?? fieldTypeName;
  }

  /** Label for a column in the left-hand list - falls back to its name, then blank (caller shows a placeholder). */
  columnLabel(column: FormGroup): string {
    return (column.get('displayName')?.value || column.get('name')?.value || '').trim();
  }

  columnSeedOf(column: FormGroup): FlexFieldData | undefined {
    return this.columnSeeds.get(column);
  }
}
