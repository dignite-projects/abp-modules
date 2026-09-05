import { CoreModule } from '@abp/ng.core';
import { Component, inject } from '@angular/core';
import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
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
import { MatrixConfiguration } from './matrix-configuration';
import { normalizeMatrixBlockTypes } from './matrix-block-type';

let nextInstanceId = 0;

/**
 * Designer-side editor for a `Matrix` field's configuration: the block-type schema. Block types, each
 * with its own field list - but each sub-field's own type-specific configuration is delegated to a
 * recursively-mounted `<ff-flex-field-config>`, the same generic dispatch every top-level field's
 * config editor already goes through.
 *
 * Three-pane master-detail, mirroring `TableConfigComponent`'s two-pane one level deeper: block types
 * on the left select which block type's own editor renders in the middle - its own name/displayName
 * plus *its* field list - and selecting a field there renders that field's full detail on the right.
 * Without this, one field type with a nested config (Select's option list, or another Table/Matrix)
 * would push every other field, of every other block type, down the page at once.
 *
 * Deliberately no drag-and-drop reordering in this first pass (`Select`'s own config editor has it, and
 * the same `@angular/cdk/drag-drop` + `moveItemInArray` pattern would apply here at two levels - block
 * types, and each block type's own fields) - add/remove is enough to be usable, reordering is a
 * reasonable follow-up rather than something this change needs to ship complete.
 */
@Component({
  selector: 'ff-matrix-config',
  templateUrl: './matrix-config.component.html',
  imports: [CoreModule, ReactiveFormsModule, FlexFieldConfigComponent],
  providers: [{ provide: COMPOSITE_NESTING_DEPTH, useFactory: nextCompositeNestingDepth }],
})
export class MatrixConfigComponent extends FieldTypeConfigBase {
  private readonly fieldTypeResolver = inject(FieldTypeResolver);

  /** The depth this matrix's own sub-fields live at - 2 for a top-level Matrix field. */
  private readonly subFieldDepth = inject(COMPOSITE_NESTING_DEPTH);

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

  /** Seed value for each sub-field row's own recursively-mounted config editor, keyed by its FormGroup
   * (a sub-field row carries no `configuration` control of its own until `<ff-flex-field-config>` adds
   * one, so the stored value has nowhere else to live between load and first render).
   *
   * Re-captured from the live form whenever a sub-field is deselected ({@link captureSeed}) - see
   * `TableConfigComponent.columnSeeds` for why a stale seed here silently reverts the admin's edits. */
  private readonly subFieldSeeds = new WeakMap<FormGroup, FlexFieldData | undefined>();

  readonly instanceId = `matrix-config-${nextInstanceId++}`;

  /** Which block type's detail (middle + right pane) is shown - null only when there are no block types. */
  selectedBlockTypeIndex: number | null = null;

  /** Which of the *selected block type's* fields is shown on the right - null only when it has none. */
  selectedSubFieldIndex: number | null = null;

  /**
   * The types a sub-field may be bound to - governed purely by nesting depth, exactly as
   * `TableConfigComponent.fieldTypeOptions` is; see that one for why there is no "a Matrix sub-field
   * cannot be a Matrix" special case and what the depth rule replaces it with.
   */
  get fieldTypeOptions(): readonly FieldTypeDefinition[] {
    if (allowsCompositeAt(this.subFieldDepth)) {
      return this.fieldTypeResolver.getAll();
    }

    const boundFieldTypeName = this.selectedSubField ? this.fieldTypeNameOf(this.selectedSubField) : '';

    return this.fieldTypeResolver
      .getAll()
      .filter(
        fieldType =>
          !this.compositeFieldTypeNames.has(fieldType.name) || fieldType.name === boundFieldTypeName,
      );
  }

  get blockTypes(): FormArray {
    return this.configuration.controls['Matrix.BlockTypes'] as FormArray;
  }

  get selectedBlockType(): FormGroup | undefined {
    return this.selectedBlockTypeIndex !== null
      ? (this.blockTypes.at(this.selectedBlockTypeIndex) as FormGroup)
      : undefined;
  }

  get selectedSubField(): FormGroup | undefined {
    const blockType = this.selectedBlockType;
    return blockType && this.selectedSubFieldIndex !== null
      ? (this.fieldsOf(blockType).at(this.selectedSubFieldIndex) as FormGroup)
      : undefined;
  }

  protected configurationDefaults(): object {
    return new MatrixConfiguration();
  }

  protected override onConfigurationPatched(): void {
    const stored = normalizeMatrixBlockTypes(this.selectedField?.configuration['Matrix.BlockTypes']);

    stored.forEach(blockType => {
      const group = this.addBlockType();
      group.patchValue({ name: blockType.name, displayName: blockType.displayName });
      blockType.fields.forEach(field => this.addSubField(group, field));
    });

    // `addBlockType`/`addSubField` leave the *last* loaded block type and field selected - default to
    // the first of each instead, the same "show something meaningful" convention as Table's own columns.
    if (this.blockTypes.length > 0) {
      this.selectBlockType(0);
    }
  }

  protected override onConfigurationReset(): void {
    // A field being created starts with no block types - unlike Select's one blank option, there is no
    // sensible default block type to guess at, so the admin adds them explicitly.
  }

  addBlockType(): FormGroup {
    const group = new FormGroup({
      name: new FormControl('', Validators.required),
      displayName: new FormControl('', Validators.required),
      fields: new FormArray<FormGroup>([]),
    });

    // Adding deselects whatever was open, so its live configuration has to be snapshotted first.
    this.captureSeed();
    this.blockTypes.push(group);
    this.selectedBlockTypeIndex = this.blockTypes.length - 1;
    this.selectedSubFieldIndex = null;
    return group;
  }

  removeBlockType(index: number): void {
    this.blockTypes.removeAt(index);

    if (this.blockTypes.length === 0) {
      this.selectedBlockTypeIndex = null;
      this.selectedSubFieldIndex = null;
    } else if (this.selectedBlockTypeIndex !== null) {
      if (index < this.selectedBlockTypeIndex) {
        this.selectedBlockTypeIndex -= 1;
      } else if (index === this.selectedBlockTypeIndex) {
        this.selectBlockType(Math.min(index, this.blockTypes.length - 1));
      }
    }
  }

  selectBlockType(index: number): void {
    // Switching block type unmounts the currently-shown sub-field's editor too, so its live
    // configuration has to be snapshotted here as well, not just in `selectSubField`.
    this.captureSeed();
    this.selectedBlockTypeIndex = index;
    const fields = this.fieldsOf(this.blockTypes.at(index));
    this.selectedSubFieldIndex = fields.length > 0 ? 0 : null;
  }

  selectSubField(index: number): void {
    this.captureSeed();
    this.selectedSubFieldIndex = index;
  }

  /**
   * Snapshots the currently-selected sub-field's *current* nested configuration into
   * {@link subFieldSeeds}, so re-selecting it re-seeds from what the admin last had on screen rather
   * than from what was loaded off the server. Writes a new object only on deselection, never per
   * change-detection cycle - see `TableConfigComponent.captureSeed` for why that matters.
   */
  private captureSeed(): void {
    const subField = this.selectedSubField;
    const configuration = subField?.get('configuration')?.value as Record<string, unknown> | undefined;
    if (!subField || !configuration) {
      return;
    }

    this.subFieldSeeds.set(subField, {
      id: '',
      name: subField.get('name')?.value ?? '',
      displayName: subField.get('displayName')?.value ?? '',
      description: subField.get('description')?.value ?? undefined,
      fieldTypeName: this.fieldTypeNameOf(subField),
      configuration,
    });
  }

  fieldsOf(blockType: AbstractControl): FormArray {
    return (blockType as FormGroup).controls['fields'] as FormArray;
  }

  addSubField(blockType: FormGroup, seed?: InlineFieldDefinition): FormGroup {
    const group = new FormGroup({
      name: new FormControl(seed?.name ?? '', Validators.required),
      displayName: new FormControl(seed?.displayName ?? '', Validators.required),
      description: new FormControl(seed?.description ?? ''),
      fieldTypeName: new FormControl(seed?.fieldTypeName ?? '', Validators.required),
      required: new FormControl(seed?.required ?? false),
      // Carries the stored configuration for a sub-field the admin never opens - see the identical
      // control in `TableConfigComponent.addColumn` for why leaving it out loses data on save.
      configuration: new FormControl(seed?.configuration ?? {}),
    });

    this.subFieldSeeds.set(
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
    this.captureSeed();
    this.fieldsOf(blockType).push(group);
    this.selectedSubFieldIndex = this.fieldsOf(blockType).length - 1;
    return group;
  }

  removeSubField(blockType: AbstractControl, index: number): void {
    this.fieldsOf(blockType).removeAt(index);

    const fields = this.fieldsOf(blockType);
    if (fields.length === 0) {
      this.selectedSubFieldIndex = null;
    } else if (this.selectedSubFieldIndex !== null) {
      if (index < this.selectedSubFieldIndex) {
        this.selectedSubFieldIndex -= 1;
      } else if (index === this.selectedSubFieldIndex) {
        this.selectedSubFieldIndex = Math.min(index, fields.length - 1);
      }
    }
  }

  fieldTypeNameOf(subField: AbstractControl): string {
    return (subField as FormGroup).get('fieldTypeName')?.value ?? '';
  }

  /** Localization key for a sub-field's field type, for the field list's subtitle. */
  fieldTypeDisplayNameKeyOf(subField: AbstractControl): string {
    const fieldTypeName = this.fieldTypeNameOf(subField);
    return this.fieldTypeResolver.find(fieldTypeName)?.displayNameKey ?? fieldTypeName;
  }

  /** Label for a block type in the left-hand list - falls back to its name, then blank. */
  blockTypeLabel(blockType: FormGroup): string {
    return (blockType.get('displayName')?.value || blockType.get('name')?.value || '').trim();
  }

  /** Label for a sub-field in the field list - falls back to its name, then blank. */
  subFieldLabel(subField: AbstractControl): string {
    const group = subField as FormGroup;
    return (group.get('displayName')?.value || group.get('name')?.value || '').trim();
  }

  subFieldSeedOf(subField: AbstractControl): FlexFieldData | undefined {
    return this.subFieldSeeds.get(subField as FormGroup);
  }
}
