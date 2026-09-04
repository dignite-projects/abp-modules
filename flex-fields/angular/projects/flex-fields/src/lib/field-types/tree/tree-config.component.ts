import { ChangeDetectorRef, Component, ElementRef, ViewChild, inject } from '@angular/core';
import { CoreModule, LocalizationService } from '@abp/ng.core';
import { TreeModule } from '@abp/ng.components/tree';
import { ThemeSharedModule } from '@abp/ng.theme.shared';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { NzFormatBeforeDropEvent } from 'ng-zorro-antd/tree';
import { Observable, of } from 'rxjs';
import { FLEX_FIELD_SLUG_GENERATOR } from '../../utils';
import { FieldTypeConfigBase } from '../field-type-config-base';
import {
  TreeNode,
  checkAncestors,
  checkedKeys,
  clearChecked,
  findTreeNode,
  keyExists,
  removeTreeNode,
  toNodeItems,
  toTreeNodes,
  toggleExpandedKey,
} from './tree-node';
import { TreeConfiguration } from './tree-configuration';

let nextCheckboxId = 0;

/** Designer-side editor for a `Tree` field: builds the node tree the field offers. */
@Component({
  selector: 'ff-tree-config',
  templateUrl: './tree-config.component.html',
  imports: [CoreModule, ThemeSharedModule, ReactiveFormsModule, TreeModule],
  styles: `
    /* This component had no styles of its own, so every bit of ng-zorro chrome came through
       untouched - all of it hardcoded light-mode: .ant-tree paints an opaque background: #fff (a
       white box in a dark host), and both node hover and the post-click "active" node paint
       #f5f5f5. Node text, by contrast, follows the host, because @abp/ng.components abp-tree sets
       .ant-tree { color: inherit } - so a hovered row in a dark host went light-on-white and the
       label vanished.

       tree-picker-nodes.component.scss documents the same ng-zorro defaults for the field picker.
       The treatment differs only in what gets painted: the picker highlights its own inline node
       body, because its template root is a d-inline-flex chip with padding and a corner radius.
       This tree node template is a plain full-width div, so the wrapper - which abp-tree already
       stretches to width: 100% - is the surface to paint here, matching the full-row highlight the
       control has always had, only in a colour that tracks the host.

       !important on the background, as in the picker: ng-zorro tree CSS is a global stylesheet and
       this keeps the override independent of load order rather than resting on the encapsulation
       attribute raising specificity. */
    :host ::ng-deep .ant-tree {
      background: transparent !important;
    }

    :host ::ng-deep .ant-tree .ant-tree-node-content-wrapper:hover,
    :host ::ng-deep .ant-tree .ant-tree-treenode-active .ant-tree-node-content-wrapper {
      background-color: var(--bs-secondary-bg, #f5f5f5) !important;
    }
  `,
})
export class TreeConfigComponent extends FieldTypeConfigBase {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly localization = inject(LocalizationService);
  private readonly slugify = inject(FLEX_FIELD_SLUG_GENERATOR);

  readonly multipleId = `ff-tree-multiple-${nextCheckboxId++}`;

  @ViewChild('nodeModalSubmit', { static: false }) nodeModalSubmit?: ElementRef<HTMLButtonElement>;

  nodes: TreeNode[] = [];
  expandedKeys: string[] = [];

  /** The node being edited, or null when a new root node is being added. */
  editingNode: TreeNode | null = null;
  isCreatingChild = false;
  isModalVisible = false;
  modalBusy = false;
  nodeForm?: FormGroup;

  get treeNodesControl(): AbstractControl {
    return this.configuration.controls['Tree.Nodes'];
  }

  get keyInput(): FormControl {
    return this.nodeForm?.get('key') as FormControl;
  }

  protected configurationDefaults(): object {
    return new TreeConfiguration();
  }

  protected override onConfigurationPatched(): void {
    // A tree field with no nodes offers nothing to pick, so the node list is required.
    this.treeNodesControl?.setValidators([Validators.required, Validators.minLength(1)]);
    this.nodes = toTreeNodes(this.selectedField?.configuration['Tree.Nodes']);
    this.syncNodesToForm();
  }

  protected override onConfigurationReset(): void {
    this.treeNodesControl?.setValidators([Validators.required, Validators.minLength(1)]);
    this.treeNodesControl?.updateValueAndValidity();
    this.nodes = [];
  }

  // --- expansion -----------------------------------------------------------------------------

  onExpandChange(event: { node?: { key?: string } }): void {
    this.expandedKeys = toggleExpandedKey(this.expandedKeys, event.node?.key);
  }

  // --- default selection ---------------------------------------------------------------------

  toggleChecked(event: Event, node: { key: string; origin?: TreeNode }): void {
    event.stopPropagation();

    const checked = !node.origin?.isChecked;

    if (!this.isMultiple) {
      clearChecked(this.nodes);
    }

    const target = findTreeNode(this.nodes, node.key);
    if (target) {
      target.isChecked = checked;
    }

    // Selecting a child implies its ancestors — a checked leaf should never read as orphaned
    // under unchecked parents.
    if (this.isMultiple && checked) {
      checkAncestors(this.nodes, node.key);
    }

    this.nodes = [...this.nodes];
    this.syncNodesToForm();
    this.cdRef.detectChanges();
  }

  isIndeterminate(node: { key: string; origin?: TreeNode }): boolean {
    if (node.origin?.isChecked) {
      return false;
    }

    const target = findTreeNode(this.nodes, node.key);

    return !!target?.children?.length && checkedKeys(target.children).length > 0;
  }

  private get isMultiple(): boolean {
    return !!this.configuration?.controls['Tree.Multiple']?.value;
  }

  /** Switching a multi-select tree back to single leaves at most one default checked. */
  onMultipleChange(): void {
    if (!this.isMultiple && checkedKeys(this.nodes).length > 1) {
      const [first] = checkedKeys(this.nodes);
      clearChecked(this.nodes);
      const target = findTreeNode(this.nodes, first);
      if (target) {
        target.isChecked = true;
      }
      this.nodes = [...this.nodes];
      this.syncNodesToForm();
    }
  }

  // --- node CRUD -----------------------------------------------------------------------------

  addNode(): void {
    this.openModal(null, false);
  }

  editNode(node: { origin?: TreeNode }): void {
    this.openModal(node.origin ?? null, false);
  }

  addChildNode(node: { origin?: TreeNode }): void {
    this.openModal(node.origin ?? null, true);
  }

  deleteNode(node: { key: string }): void {
    removeTreeNode(this.nodes, node.key);
    this.nodes = [...this.nodes];
    this.syncNodesToForm();
    this.cdRef.detectChanges();
  }

  // --- drag & drop -----------------------------------------------------------------------------

  /**
   * Reparents/reorders a node in place. `pos` is `abp-tree`'s own convention: 0 drops inside the
   * target (becomes its last child), 1 drops after it, -1 drops before it.
   */
  beforeDrop = (event: NzFormatBeforeDropEvent): Observable<boolean> => {
    const { pos, dragNode, node } = event;

    if (![0, 1, -1].includes(pos)) {
      return of(false);
    }

    const dragged = findTreeNode(this.nodes, dragNode.key);
    if (!dragged || findTreeNode(dragged.children, node.key)) {
      // Not found, or dropping onto one of its own descendants would create a cycle.
      return of(false);
    }

    removeTreeNode(this.nodes, dragNode.key);

    if (pos === 0) {
      const target = findTreeNode(this.nodes, node.key);
      target?.children.push(dragged);
    } else {
      const parent = node.parentNode;
      // The gap before an expanded node's first child visually reads as "inside the parent,
      // before the first child," but nz-tree reports it identically to "before the parent's own
      // next sibling" - treat it as the latter, or dropping there would be a confusing second way
      // to land as the parent's first child.
      const isIndentedFirstChildGap = pos === -1 && !!parent && parent.children[0]?.key === node.key;

      const afterKey = isIndentedFirstChildGap ? parent!.key : node.key;
      const parentKey = isIndentedFirstChildGap ? parent!.parentNode?.key : parent?.key;
      const siblings = parentKey ? findTreeNode(this.nodes, parentKey)?.children : this.nodes;
      const index = siblings?.findIndex(sibling => sibling.key === afterKey) ?? -1;
      const insertAfter = isIndentedFirstChildGap || pos === 1;

      siblings?.splice(index < 0 ? siblings.length : insertAfter ? index + 1 : index, 0, dragged);
    }

    this.nodes = [...this.nodes];
    this.syncNodesToForm();
    this.cdRef.detectChanges();

    // We already applied the move to `this.nodes` ourselves; returning `false` tells abp-tree not
    // to also apply its own local reorder on top of it.
    return of(false);
  };

  private openModal(node: TreeNode | null, creatingChild: boolean): void {
    this.isModalVisible = true;
    this.isCreatingChild = creatingChild;
    this.editingNode = node;

    const editing = node && !creatingChild ? node : null;

    this.nodeForm = new FormGroup({
      title: new FormControl(editing?.title ?? '', Validators.required),
      key: new FormControl(editing?.key ?? '', [Validators.required, this.keyValidator()]),
      isChecked: new FormControl(editing?.isChecked ?? false),
    });
  }

  closeModal(): void {
    this.isModalVisible = false;
    this.isCreatingChild = false;
    this.editingNode = null;
  }

  save(): void {
    if (!this.nodeForm?.valid) {
      return;
    }

    const { title, key, isChecked } = this.nodeForm.value;
    const editing = this.editingNode;

    if (editing && this.isCreatingChild) {
      editing.children = [
        ...(editing.children ?? []),
        { title, key, entity: { key }, isChecked, children: [] },
      ];
      // Adding a child to a collapsed node would otherwise appear to do nothing.
      if (!this.expandedKeys.includes(editing.key)) {
        this.expandedKeys = [...this.expandedKeys, editing.key];
      }
    } else if (editing) {
      editing.title = title;
      editing.key = key;
      editing.entity = { key };
      editing.isChecked = isChecked;
    } else {
      this.nodes.push({ title, key, entity: { key }, isChecked, children: [] });
    }

    if (!this.isMultiple && isChecked) {
      clearChecked(this.nodes);
      const target = findTreeNode(this.nodes, key);
      if (target) {
        target.isChecked = true;
      }
    } else if (this.isMultiple && isChecked) {
      checkAncestors(this.nodes, key);
    }

    this.nodes = [...this.nodes];
    this.syncNodesToForm();
    this.cdRef.detectChanges();
    this.closeModal();
  }

  /** Suggests a value from the label, but never overwrites one the user has typed. */
  onTitleBlur(event: Event): void {
    const key = this.nodeForm?.get('key');

    if (!key || key.value) {
      return;
    }

    key.patchValue(this.slugify((event.target as HTMLInputElement).value));
  }

  private keyValidator() {
    return (control: AbstractControl): ValidationErrors | null => {
      if (!control.value) {
        return null;
      }

      if (!/^[a-zA-Z0-9_-]+$/.test(control.value)) {
        return {
          repetition: this.localization.instant('FlexFields::Validate:InvalidNodeValue'),
        };
      }

      // Editing a node must not collide with itself.
      if (keyExists(this.nodes, control.value, this.editingNode?.key)) {
        return {
          repetition: this.localization.instant('FlexFields::Validate:NodeValueAlreadyExists'),
        };
      }

      return null;
    };
  }

  /** Writes the view-model tree back into the stored `Tree.Nodes` value. */
  private syncNodesToForm(): void {
    this.configuration.patchValue({ 'Tree.Nodes': toNodeItems(this.nodes) });
    this.treeNodesControl?.updateValueAndValidity();
  }
}
