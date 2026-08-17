import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { readStringList } from '../../utils';
import { TreePickerBase } from './tree-picker-base';
import { TreePickerNodesComponent } from './tree-picker-nodes.component';
import { checkedKeys, clearChecked, findTreeNode, toTreeNodes } from './tree-node';

/** Edits the value of a `Tree` field with an always-expanded, inline tree picker. */
@Component({
  selector: 'ff-tree-control',
  templateUrl: './tree-control.component.html',
  imports: [CommonModule, ReactiveFormsModule, TreePickerNodesComponent],
})
export class TreeControlComponent extends TreePickerBase {
  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];

    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    this.nodes = toTreeNodes(this.fieldValue!.field.configuration['Tree.Nodes']);

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
}
