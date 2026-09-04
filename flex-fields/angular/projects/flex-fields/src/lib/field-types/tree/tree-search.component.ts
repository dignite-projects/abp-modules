import { Component, ElementRef, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, ReactiveFormsModule } from '@angular/forms';
import { readStringList } from '../../utils';
import { TreePickerBase } from './tree-picker-base';
import { TreePickerNodesComponent } from './tree-picker-nodes.component';
import { findTreeNode, toTreeNodes } from './tree-node';

/** Filters by a `Tree` field, using a dropdown tree picker. */
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
        /* --lpx-content-bg is full-LeptonX only; LeptonX Lite never defines it, so this used to
           fall through to #fff and the panel stayed white in every theme while the color above
           followed --bs-body-color into light-on-white. --bs-secondary-bg flips with the host.
           See select-field.component.scss for the same chain and the full reasoning. */
        background-color: var(--lpx-content-bg, var(--bs-secondary-bg, #fff));
        /* Hardcoded black at 12% was invisible against the dark panel this rule now paints.
           --bs-border-color is what every other Bootstrap-based control on the page uses and it
           flips with the host (#e7e9ec -> #495057 in LeptonX Lite). */
        border: 1px solid var(--bs-border-color, rgba(0, 0, 0, 0.12));
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
    this.nodes = toTreeNodes(this.fieldValue!.field.configuration['Tree.Nodes']);

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
