import { Directive } from '@angular/core';
import { FieldTypeControlBase } from '../field-type-control-base';
import { TreeNode, ancestorKeys, toggleExpandedKey } from './tree-node';
import { TreeViewConfiguration } from './tree-view-configuration';

/**
 * Shared value-selection logic for the two components that let an end user *pick* values out of a
 * `TreeView` field's node tree: the inline {@link TreeControlComponent} and the dropdown
 * {@link TreeSearchComponent}. A subclass supplies only its own wrapper markup, `createControl()`
 * (they seed `selectedKeys` from a different source), and {@link onSingleSelected} for any extra
 * behavior a single pick should trigger.
 *
 * Node rendering itself (checkbox/highlight state, click handling) lives in
 * {@link TreePickerNodesComponent}, not here — that is what keeps both pickers' trees visually and
 * behaviorally identical instead of two hand-maintained copies.
 */
@Directive()
export abstract class TreePickerBase extends FieldTypeControlBase {
  nodes: TreeNode[] = [];
  expandedKeys: string[] = [];
  selectedKeys: string[] = [];

  get multiple(): boolean {
    return !!this.fieldValue?.field.configuration['TreeView.Multiple'];
  }

  protected configurationDefaults(): object {
    return new TreeViewConfiguration();
  }

  onExpandChange(event: { node?: { key?: string } }): void {
    this.expandedKeys = toggleExpandedKey(this.expandedKeys, event.node?.key);
  }

  clearSelection(): void {
    this.selectedKeys = [];
    this.fieldControl?.patchValue(this.multiple ? [] : null);
    this.fieldControl?.markAsDirty();
    this.fieldControl?.markAsTouched();
  }

  toggleSelected(key: string): void {
    if (this.multiple) {
      this.toggleMultiple(key);
      return;
    }

    this.selectedKeys = this.selectedKeys.includes(key) ? [] : [key];
    this.fieldControl?.patchValue(this.selectedKeys[0] ?? null);
    this.fieldControl?.markAsDirty();
    this.fieldControl?.markAsTouched();
    this.onSingleSelected();
  }

  /** Extra behavior when a single value is picked — the search box closes its dropdown. */
  protected onSingleSelected(): void {}

  private toggleMultiple(key: string): void {
    const checking = !this.selectedKeys.includes(key);
    const next = new Set(this.selectedKeys);

    if (checking) {
      next.add(key);
      // Selecting a child implies its ancestors — a checked leaf should never read as orphaned
      // under unchecked parents.
      ancestorKeys(this.nodes, key).forEach(ancestor => next.add(ancestor));
    } else {
      next.delete(key);
    }

    this.selectedKeys = [...next];
    this.fieldControl?.patchValue(this.selectedKeys);
    this.fieldControl?.markAsDirty();
    this.fieldControl?.markAsTouched();
  }
}
