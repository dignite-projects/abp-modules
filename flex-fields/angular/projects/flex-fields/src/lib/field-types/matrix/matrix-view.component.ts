import { CommonModule } from '@angular/common';
import { Component, Input, OnChanges } from '@angular/core';
import { FlexFieldViewComponent } from '../../components/flex-field-view.component';
import type { FlexFieldValue } from '../../models';
import type { InlineFieldDefinition } from '../inline-field-definition';
import type { MatrixBlockType, MatrixBlockValue } from './matrix-block-type';
import { normalizeMatrixBlockTypes, normalizeMatrixBlockValues } from './matrix-block-type';

/**
 * Displays the value of a `Matrix` field read-only: iterates block instances and, for each sub-field,
 * recursively invokes `<ff-flex-field-view>` - reusing the existing top-level dispatcher one level
 * deeper rather than hand-writing rendering for every field type that might show up inside a block.
 */
@Component({
  selector: 'ff-matrix-view',
  templateUrl: './matrix-view.component.html',
  imports: [CommonModule, FlexFieldViewComponent],
})
export class MatrixViewComponent implements OnChanges {
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type - always `Matrix` here. */
  @Input() type?: string;

  @Input() value: unknown = '';

  blocks: MatrixBlockValue[] = [];
  blockTypes: MatrixBlockType[] = [];

  /** `subFieldValueOf` is called from the template on every change-detection cycle, and
   * `<ff-flex-field-view>` only keeps its mounted child alive across a cycle if `[fields]` is
   * reference-equal to what it rendered last time - a fresh object literal per call defeats that and
   * tears down/rebuilds every recursively-mounted view on every cycle, not just when the value actually
   * changes. Keyed on `block` (outer) then sub-field name (inner): `ngOnChanges` replaces `this.blocks`
   * wholesale on a real value change, so stale entries for old block objects simply stop being reachable
   * rather than needing explicit invalidation. */
  private readonly subFieldValueCache = new WeakMap<MatrixBlockValue, Map<string, FlexFieldValue>>();

  ngOnChanges(): void {
    this.blocks = normalizeMatrixBlockValues(this.value);
    this.blockTypes = normalizeMatrixBlockTypes(this.fields?.field.configuration['Matrix.BlockTypes']);
  }

  blockTypeOf(block: MatrixBlockValue): MatrixBlockType | undefined {
    return this.blockTypes.find(blockType => blockType.name === block.blockTypeName);
  }

  subFieldValueOf(subField: InlineFieldDefinition, block: MatrixBlockValue): FlexFieldValue {
    let blockCache = this.subFieldValueCache.get(block);
    if (!blockCache) {
      blockCache = new Map();
      this.subFieldValueCache.set(block, blockCache);
    }

    let value = blockCache.get(subField.name);
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
        value: block.values[subField.name],
      };
      blockCache.set(subField.name, value);
    }
    return value;
  }
}
