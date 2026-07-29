import { ChangeDetectorRef, Component, ElementRef, inject } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { TreeModule } from '@abp/ng.components/tree';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { readStringList } from '../../utils';
import { FieldTypeControlBase } from '../field-type-control-base';
import {
  TreeNode,
  checkAncestors,
  checkedKeys,
  clearChecked,
  expandableKeys,
  findTreeNode,
  setCheckedCascading,
  toTreeNodes,
  walkTreeNodes,
} from './tree-node';
import { TreeViewConfiguration } from './tree-view-configuration';

/** Edits the value of a `TreeView` field — a checkable tree of the configured nodes. */
@Component({
  selector: 'ff-tree-control',
  templateUrl: './tree-control.component.html',
  imports: [CoreModule, ReactiveFormsModule, TreeModule],
})
export class TreeControlComponent extends FieldTypeControlBase {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly host = inject(ElementRef<HTMLElement>);

  nodes: TreeNode[] = [];
  expandedKeys: string[] = [];
  isAllExpanded = false;

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

    return this.fb.control(checkedKeys(this.nodes), validators);
  }

  hasExpandableNodes(): boolean {
    return expandableKeys(this.nodes).length > 0;
  }

  onExpandChange(event: { node?: { key?: string } }): void {
    const key = event.node?.key;

    if (!key) {
      return;
    }

    this.expandedKeys = this.expandedKeys.includes(key)
      ? this.expandedKeys.filter(expanded => expanded !== key)
      : [...this.expandedKeys, key];

    const expandable = expandableKeys(this.nodes);
    this.isAllExpanded =
      expandable.length > 0 && expandable.every(candidate => this.expandedKeys.includes(candidate));
  }

  toggleExpandAll(): void {
    this.isAllExpanded = !this.isAllExpanded;
    this.expandedKeys = this.isAllExpanded ? expandableKeys(this.nodes) : [];

    if (!this.isAllExpanded) {
      walkTreeNodes(this.nodes, node => {
        node.expanded = false;
      });
      this.nodes = [...this.nodes];
    }

    this.cdRef.detectChanges();
  }

  toggleChecked(event: Event, node: { key: string; origin?: TreeNode }): void {
    event.stopPropagation();

    const checked = !node.origin?.isChecked;

    if (this.multiple) {
      setCheckedCascading(this.nodes, node.key, checked);
      if (checked) {
        checkAncestors(this.nodes, node.key);
      }
    } else {
      clearChecked(this.nodes);
      if (checked) {
        const target = findTreeNode(this.nodes, node.key);
        if (target) {
          target.isChecked = true;
        }
      }
    }

    this.nodes = [...this.nodes];
    this.fieldControl?.setValue(checkedKeys(this.nodes));
    this.syncIndeterminate();
    this.cdRef.detectChanges();
  }

  /** A node is indeterminate when it is unchecked but something beneath it is checked. */
  isIndeterminate(node: { key: string; origin?: TreeNode }): boolean {
    if (node.origin?.isChecked) {
      return false;
    }

    const target = findTreeNode(this.nodes, node.key);

    if (!target?.children?.length) {
      return false;
    }

    return checkedKeys(target.children).length > 0;
  }

  /**
   * `indeterminate` is a DOM property with no HTML attribute, so it cannot be set by binding. Scoped
   * to this component's own element — the old library ran `document.querySelectorAll` on every change
   * detection pass, which reached every checkbox on the page, not just its own.
   */
  private syncIndeterminate(): void {
    const checkboxes = (this.host.nativeElement as HTMLElement).querySelectorAll<HTMLInputElement>(
      'input[type="checkbox"][data-indeterminate]',
    );

    checkboxes.forEach(checkbox => {
      checkbox.indeterminate = checkbox.getAttribute('data-indeterminate') === 'true';
    });
  }
}
