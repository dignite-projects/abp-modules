import { Component, ElementRef, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, ReactiveFormsModule } from '@angular/forms';
import { readStringList } from '../../utils';
import { TreePickerBase } from './tree-picker-base';
import { TreePickerNodesComponent } from './tree-picker-nodes.component';
import { findTreeNode, toTreeNodes } from './tree-node';

/** Filters by a `TreeView` field, using a dropdown tree picker. */
@Component({
  selector: 'ff-tree-search',
  templateUrl: './tree-search.component.html',
  styles: [
    `
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
        color: var(--bs-body-color);
        background-color: var(--lpx-content-bg, #fff);
        border: 1px solid rgba(0, 0, 0, 0.12);
        border-radius: 0.375rem;
        box-shadow: 0 0.125rem 0.25rem rgba(0, 0, 0, 0.075);
      }
    `,
  ],
  imports: [CommonModule, ReactiveFormsModule, TreePickerNodesComponent],
})
export class TreeSearchComponent extends TreePickerBase {
  private readonly elementRef = inject<ElementRef<HTMLElement>>(ElementRef);

  isDropdownOpen = false;

  get displayText(): string {
    return this.selectedLabels().join(', ');
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

  toggleDropdown(): void {
    this.isDropdownOpen = !this.isDropdownOpen;
    this.fieldControl?.markAsTouched();
  }

  override clearSelection(event?: Event): void {
    event?.stopPropagation();
    super.clearSelection();
  }

  protected override onSingleSelected(): void {
    this.isDropdownOpen = false;
  }

  private selectedLabels(): string[] {
    return this.selectedKeys.map(key => findTreeNode(this.nodes, key)?.title ?? key);
  }

  private isClickInsideComponent(event: MouseEvent): boolean {
    const path = event.composedPath?.() ?? [];

    return path.some(target => target === this.elementRef.nativeElement);
  }
}
