import { Component, ElementRef, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TreeModule } from '@abp/ng.components/tree';
import { AbstractControl, ReactiveFormsModule } from '@angular/forms';
import { readStringList } from '../../utils';
import { FieldTypeControlBase } from '../field-type-control-base';
import { TreeNode, ancestorKeys, findTreeNode, toTreeNodes } from './tree-node';
import { TreeViewConfiguration } from './tree-view-configuration';

/** Filters by a `TreeView` field, using a dropdown tree picker. */
@Component({
  selector: 'ff-tree-search',
  templateUrl: './tree-search.component.html',
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

      .ff-tree-picker {
        position: relative;
      }

      .ff-tree-picker-toggle {
        height: auto;
        min-height: calc(1.5em + 1.35rem + 2px);
        padding-right: 3.75rem;
        overflow: hidden;
        text-overflow: ellipsis;
        white-space: nowrap;
      }

      .ff-tree-picker-clear {
        position: absolute;
        top: 50%;
        right: 2rem;
        display: inline-flex;
        align-items: center;
        justify-content: center;
        height: 100%;
        padding: 0 0.375rem;
        line-height: 1;
        color: var(--bs-secondary-color, #6c757d);
        text-decoration: none;
        transform: translateY(-50%);
        z-index: 2;
      }

      .ff-tree-picker-menu {
        position: absolute;
        z-index: 1050;
        width: 100%;
        max-height: 18rem;
        margin-top: 0.25rem;
        padding: 0.375rem;
        overflow: auto;
        color: rgba(0, 0, 0, 0.85);
        background-color: #fff;
        border: 1px solid rgba(0, 0, 0, 0.12);
        border-radius: 0.375rem;
        box-shadow: 0 0.125rem 0.25rem rgba(0, 0, 0, 0.075);
      }
    `,
  ],
  imports: [CommonModule, ReactiveFormsModule, TreeModule],
})
export class TreeSearchComponent extends FieldTypeControlBase {
  private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);

  nodes: TreeNode[] = [];
  expandedKeys: string[] = [];
  selectedKeys: string[] = [];
  isDropdownOpen = false;

  get multiple(): boolean {
    return !!this.fieldValue?.field.configuration['TreeView.Multiple'];
  }

  get displayText(): string {
    return this.selectedLabels().join(', ');
  }

  protected configurationDefaults(): object {
    return new TreeViewConfiguration();
  }

  protected createControl(): AbstractControl {
    this.nodes = toTreeNodes(this.fieldValue!.field.configuration['TreeView.Nodes']);

    // Nodes marked `Selected` in the configuration are a default *answer*, not a default filter.
    const stored = readStringList(this.selectedValue).filter(value => value !== '');
    this.selectedKeys = this.multiple ? stored : stored.slice(0, 1);

    return this.fb.control(this.multiple ? this.selectedKeys : (this.selectedKeys[0] ?? null));
  }

  @HostListener('document:click', ['$event'])
  closeDropdownOnOutsideClick(event: MouseEvent): void {
    if (!this.isDropdownOpen) {
      return;
    }

    if (!this.isClickInsideComponent(event)) {
      this.isDropdownOpen = false;
    }
  }

  onTreeNodeChange(node: { key?: string } | undefined): void {
    if (node?.key) {
      this.selectKey(node.key);
    }
  }

  toggleDropdown(): void {
    this.isDropdownOpen = !this.isDropdownOpen;
    this.fieldControl?.markAsTouched();
  }

  clearSelection(event: Event): void {
    event.stopPropagation();
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
    this.isDropdownOpen = false;
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

  private selectedLabels(): string[] {
    return this.selectedKeys.map(key => findTreeNode(this.nodes, key)?.title ?? key);
  }

  private isClickInsideComponent(event: MouseEvent): boolean {
    const path = event.composedPath?.() ?? [];

    return path.some(target => target === this.elementRef.nativeElement);
  }
}
