import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TreeModule } from '@abp/ng.components/tree';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { readStringList } from '../../utils';
import { FieldTypeControlBase } from '../field-type-control-base';
import {
  TreeNode,
  ancestorKeys,
  checkedKeys,
  clearChecked,
  findTreeNode,
  toTreeNodes,
} from './tree-node';
import { TreeViewConfiguration } from './tree-view-configuration';

/** Edits the value of a `TreeView` field with an always-expanded, inline tree picker. */
@Component({
  selector: 'ff-tree-control',
  templateUrl: './tree-control.component.html',
  styles: [
    `
      .ff-tree-picker-node {
        min-height: 1.75rem;
        padding: 0.125rem 0.375rem;
        border-radius: 0.25rem;
      }

      .ff-tree-picker-node-selected {
        background-color: var(--bs-primary-bg-subtle, rgba(13, 110, 253, 0.12));
        color: var(--bs-primary-text-emphasis, #052c65);
      }
    `,
  ],
  imports: [CommonModule, ReactiveFormsModule, TreeModule],
})
export class TreeControlComponent extends FieldTypeControlBase {
  nodes: TreeNode[] = [];
  expandedKeys: string[] = [];
  selectedKeys: string[] = [];

  get multiple(): boolean {
    return !!this.fieldValue?.field.configuration['TreeView.Multiple'];
  }

  protected configurationDefaults(): object {
    return new TreeViewConfiguration();
  }

  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];

    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    this.nodes = toTreeNodes(this.fieldValue!.field.configuration['TreeView.Nodes']);

    const stored = readStringList(this.selectedValue).filter(value => value !== '');

    if (stored.length > 0) {
      // Only the nodes the stored value names — not their children. Checking a parent in the designer
      // means "this is the default"; it does not mean every descendant was chosen.
      clearChecked(this.nodes);
      stored.forEach(key => {
        const node = findTreeNode(this.nodes, key);
        if (node) {
          node.isChecked = true;
        }
      });
    }

    const selected = checkedKeys(this.nodes);
    this.selectedKeys = this.multiple ? selected : selected.slice(0, 1);
    return this.fb.control(this.multiple ? selected : (selected[0] ?? null), validators);
  }

  onTreeNodeChange(node: { key?: string } | undefined): void {
    if (node?.key) {
      this.selectKey(node.key);
    }
  }

  clearSelection(): void {
    this.selectedKeys = [];
    this.fieldControl?.patchValue(this.multiple ? [] : null);
    this.fieldControl?.markAsDirty();
    this.fieldControl?.markAsTouched();
  }

  toggleSelected(event: Event, node: { key: string; origin?: TreeNode }): void {
    event.stopPropagation();
    this.selectKey(node.key);
  }

  private selectKey(key: string): void {
    if (this.multiple) {
      this.toggleMultiple(key);
      return;
    }

    this.selectedKeys = this.selectedKeys.includes(key) ? [] : [key];
    this.fieldControl?.patchValue(this.selectedKeys[0] ?? null);
    this.fieldControl?.markAsDirty();
    this.fieldControl?.markAsTouched();
  }

  isNodeSelected = (node: { key: string }): boolean => {
    return !this.multiple && this.selectedKeys.includes(node.key);
  };

  isNodeChecked(node: { key: string }): boolean {
    return this.selectedKeys.includes(node.key);
  }

  isNodeActive(node: { key: string }): boolean {
    return this.selectedKeys.includes(node.key);
  }

  isIndeterminate(node: { key: string; origin?: TreeNode }): boolean {
    if (!this.multiple || this.isNodeChecked(node)) {
      return false;
    }

    const target = findTreeNode(this.nodes, node.key);

    return !!target?.children?.length && this.hasCheckedDescendant(target.children);
  }

  onExpandChange(event: { node?: { key?: string } }): void {
    const key = event.node?.key;

    if (!key) {
      return;
    }

    this.expandedKeys = this.expandedKeys.includes(key)
      ? this.expandedKeys.filter(expanded => expanded !== key)
      : [...this.expandedKeys, key];
  }

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

  private hasCheckedDescendant(nodes: TreeNode[]): boolean {
    return nodes.some(
      node => this.selectedKeys.includes(node.key) || this.hasCheckedDescendant(node.children ?? []),
    );
  }
}
