import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { readStringList } from '../../utils';
import { FieldTypeControlBase } from '../field-type-control-base';
import {
  FlatTreeNode,
  TreeNode,
  checkedKeys,
  clearChecked,
  findTreeNode,
  flattenTreeNodes,
  toTreeNodes,
} from './tree-node';
import { TreeViewConfiguration } from './tree-view-configuration';

/** Edits the value of a `TreeView` field with a dropdown tree picker. */
@Component({
  selector: 'ff-tree-control',
  templateUrl: './tree-control.component.html',
  imports: [CommonModule, ReactiveFormsModule],
})
export class TreeControlComponent extends FieldTypeControlBase {
  nodes: TreeNode[] = [];
  options: FlatTreeNode[] = [];

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
    this.options = flattenTreeNodes(this.nodes);

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
    return this.fb.control(this.multiple ? selected : (selected[0] ?? null), validators);
  }
}
