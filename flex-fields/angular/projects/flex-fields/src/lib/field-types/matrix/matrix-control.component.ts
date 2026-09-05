import { CoreModule, LocalizationService } from '@abp/ng.core';
import { CommonModule } from '@angular/common';
import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidatorFn,
} from '@angular/forms';
import { Component, inject } from '@angular/core';
import { FlexFieldControlComponent } from '../../components/flex-field-control.component';
import type { FlexFieldValue } from '../../models';
import { flexFieldErrorMessage } from '../../utils';
import { FieldTypeControlBase } from '../field-type-control-base';
import type { InlineFieldDefinition } from '../inline-field-definition';
import { MatrixConfiguration } from './matrix-configuration';
import type { MatrixBlockType } from './matrix-block-type';
import { normalizeMatrixBlockTypes, normalizeMatrixBlockValues } from './matrix-block-type';

/**
 * Edits the value of a `Matrix` field: a `FormArray` of block instances, each an occurrence of one of
 * the block types `Matrix.BlockTypes` declares. One "add" button per configured block type (not a
 * single generic "add row" button) is the clearest UX difference from a plain repeatable table.
 *
 * Each block recursively mounts `<ff-flex-field-control>` per sub-field - the same generic dispatch a
 * top-level field goes through to reach this component in the first place, just invoked one level
 * deeper with the block instance's own `values` group as the new `entity`.
 */
@Component({
  selector: 'ff-matrix-control',
  templateUrl: './matrix-control.component.html',
  imports: [CoreModule, CommonModule, ReactiveFormsModule, FlexFieldControlComponent],
})
export class MatrixControlComponent extends FieldTypeControlBase {
  private readonly localization = inject(LocalizationService);

  /** Which block instances currently show their fields. UI state only - never written into the value. */
  private readonly expandedBlocks = new Set<AbstractControl>();

  /** Each block instance's stored sub-field values, keyed by its FormGroup - `values` itself starts
   * empty and is populated one control at a time as each `<ff-flex-field-control>` mounts, so the
   * original stored dictionary has to live somewhere else for `[selected]` to read from. */
  private readonly blockValueSeeds = new WeakMap<FormGroup, Record<string, unknown>>();

  /** `subFieldValueOf` is called from the template on every change-detection cycle, and
   * `<ff-flex-field-control>` only keeps its mounted child alive across a cycle if `[fields]` is
   * reference-equal to what it rendered last time - a fresh object literal per call defeats that and
   * tears down/rebuilds the recursively-mounted control (losing focus and in-progress input) on every
   * cycle, not just when the sub-field actually changes. `blockTypes` (hence each `subField`) is only
   * replaced when `createControl()` reruns, so caching by `subField` identity is safe here. */
  private readonly subFieldValueCache = new WeakMap<InlineFieldDefinition, FlexFieldValue>();

  blockTypes: MatrixBlockType[] = [];

  get blocks(): FormArray<FormGroup> {
    return (this.fieldControl as FormArray<FormGroup>) ?? this.fb.array<FormGroup>([]);
  }

  protected configurationDefaults(): object {
    return new MatrixConfiguration();
  }

  protected createControl(): AbstractControl {
    this.blockTypes = normalizeMatrixBlockTypes(
      this.fieldValue?.field.configuration['Matrix.BlockTypes'],
    );
    const stored = normalizeMatrixBlockValues(this.selectedValue);

    const validators: ValidatorFn[] = [];
    if (this.fieldValue!.required) {
      validators.push(control => ((control as FormArray).length > 0 ? null : { required: true }));
    }

    const array = this.fb.array<FormGroup>([], validators);
    stored.forEach(block => array.push(this.buildBlockGroup(block.blockTypeName, block.values, false)));
    return array;
  }

  blockTypeOf(block: AbstractControl): MatrixBlockType | undefined {
    const name = (block as FormGroup).get('blockTypeName')?.value;
    return this.blockTypes.find(blockType => blockType.name === name);
  }

  fieldsOf(block: AbstractControl): InlineFieldDefinition[] {
    return this.blockTypeOf(block)?.fields ?? [];
  }

  valuesGroupOf(block: AbstractControl): FormGroup {
    return (block as FormGroup).get('values') as FormGroup;
  }

  subFieldValueOf(subField: InlineFieldDefinition): FlexFieldValue {
    let value = this.subFieldValueCache.get(subField);
    if (!value) {
      value = {
        field: {
          id: '',
          name: subField.name,
          displayName: subField.displayName,
          description: subField.description,
          fieldTypeName: subField.fieldTypeName,
          configuration: subField.configuration,
        },
        required: subField.required,
        searchable: false,
      };
      this.subFieldValueCache.set(subField, value);
    }
    return value;
  }

  selectedValueOf(block: AbstractControl, subField: InlineFieldDefinition): unknown {
    return this.blockValueSeeds.get(block as FormGroup)?.[subField.name];
  }

  subFieldErrorMessage(block: AbstractControl, subField: InlineFieldDefinition): string | null {
    return flexFieldErrorMessage(this.valuesGroupOf(block).get(subField.name), this.localization);
  }

  addBlock(blockType: MatrixBlockType): void {
    this.blocks.push(this.buildBlockGroup(blockType.name, {}, true));
  }

  removeBlock(index: number): void {
    const group = this.blocks.at(index);
    this.blocks.removeAt(index);
    this.expandedBlocks.delete(group);
  }

  isExpanded(block: AbstractControl): boolean {
    return this.expandedBlocks.has(block);
  }

  toggleExpanded(block: AbstractControl): void {
    if (this.expandedBlocks.has(block)) {
      this.expandedBlocks.delete(block);
    } else {
      this.expandedBlocks.add(block);
    }
  }

  private buildBlockGroup(
    blockTypeName: string,
    values: Record<string, unknown>,
    startExpanded: boolean,
  ): FormGroup {
    const group = new FormGroup({
      blockTypeName: new FormControl(blockTypeName),
      values: new FormGroup({}),
    });

    this.blockValueSeeds.set(group, values);

    if (startExpanded) {
      this.expandedBlocks.add(group);
    }

    return group;
  }
}
