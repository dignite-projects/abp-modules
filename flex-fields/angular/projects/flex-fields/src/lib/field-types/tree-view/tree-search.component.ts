import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, ReactiveFormsModule } from '@angular/forms';
import { readStringList } from '../../utils';
import { FieldTypeControlBase } from '../field-type-control-base';
import { FlatTreeNode, TreeNode, flattenTreeNodes, toTreeNodes } from './tree-node';
import { TreeViewConfiguration } from './tree-view-configuration';

/** Filters by a `TreeView` field, using a dropdown tree picker. */
@Component({
  selector: 'ff-tree-search',
  templateUrl: './tree-search.component.html',
  imports: [CommonModule, ReactiveFormsModule],
})
export class TreeSearchComponent extends FieldTypeControlBase {
  nodes: TreeNode[] = [];
  options: FlatTreeNode[] = [];

  get multiple(): boolean {
    return !!this.fieldValue?.field.configuration['TreeView.Multiple'];
  }

  protected configurationDefaults(): object {
    return new TreeViewConfiguration();
  }

  protected createControl(): AbstractControl {
    this.nodes = toTreeNodes(this.fieldValue!.field.configuration['TreeView.Nodes']);
    this.options = flattenTreeNodes(this.nodes);

    // Nodes marked `Selected` in the configuration are a default *answer*, not a default filter.
    const stored = readStringList(this.selectedValue).filter(value => value !== '');

    return this.fb.control(this.multiple ? stored : (stored[0] ?? null));
  }
}
