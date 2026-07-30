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
import { FLEX_FIELD_SLUG_GENERATOR } from '../../utils';
import { FieldTypeConfigBase } from '../field-type-config-base';
import {
  TreeNode,
  checkedKeys,
  clearChecked,
  expandableKeys,
  findTreeNode,
  keyExists,
  removeTreeNode,
  toNodeItems,
  toTreeNodes,
  walkTreeNodes,
} from './tree-node';
import { TreeViewConfiguration } from './tree-view-configuration';

let nextCheckboxId = 0;

/** Designer-side editor for a `TreeView` field: builds the node tree the field offers. */
@Component({
  selector: 'ff-tree-config',
  templateUrl: './tree-config.component.html',
  imports: [CoreModule, ThemeSharedModule, ReactiveFormsModule, TreeModule],
})
export class TreeConfigComponent extends FieldTypeConfigBase {
  private readonly cdRef = inject(ChangeDetectorRef);
  private readonly localization = inject(LocalizationService);
  private readonly slugify = inject(FLEX_FIELD_SLUG_GENERATOR);

  readonly multipleId = `ff-tree-multiple-${nextCheckboxId++}`;

  @ViewChild('nodeModalSubmit', { static: false }) nodeModalSubmit?: ElementRef<HTMLButtonElement>;

  nodes: TreeNode[] = [];
  expandedKeys: string[] = [];
  isAllExpanded = false;

  /** The node being edited, or null when a new root node is being added. */
  editingNode: TreeNode | null = null;
  isCreatingChild = false;
  isModalVisible = false;
  modalBusy = false;
  nodeForm?: FormGroup;

  get treeNodesControl(): AbstractControl {
    return this.configuration.controls['TreeView.Nodes'];
  }

  get keyInput(): FormControl {
    return this.nodeForm?.get('key') as FormControl;
  }

  protected configurationDefaults(): object {
    return new TreeViewConfiguration();
  }

  protected override onConfigurationPatched(): void {
    // A tree field with no nodes offers nothing to pick, so the node list is required.
    this.treeNodesControl?.setValidators([Validators.required, Validators.minLength(1)]);
    this.nodes = toTreeNodes(this.selectedField?.configuration['TreeView.Nodes']);
    this.syncNodesToForm();
  }

  protected override onConfigurationReset(): void {
    this.treeNodesControl?.setValidators([Validators.required, Validators.minLength(1)]);
    this.nodes = [];
  }

  // --- expansion -----------------------------------------------------------------------------

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

    this.updateExpandedState();
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

  private updateExpandedState(): void {
    const expandable = expandableKeys(this.nodes);
    this.isAllExpanded =
      expandable.length > 0 && expandable.every(key => this.expandedKeys.includes(key));
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
    return !!this.configuration?.controls['TreeView.Multiple']?.value;
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
    this.updateExpandedState();
    this.cdRef.detectChanges();
  }

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
    }

    this.nodes = [...this.nodes];
    this.syncNodesToForm();
    this.updateExpandedState();
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

  /** Writes the view-model tree back into the stored `TreeView.Nodes` value. */
  private syncNodesToForm(): void {
    this.configuration.patchValue({ 'TreeView.Nodes': toNodeItems(this.nodes) });
    this.treeNodesControl?.updateValueAndValidity();
  }
}
