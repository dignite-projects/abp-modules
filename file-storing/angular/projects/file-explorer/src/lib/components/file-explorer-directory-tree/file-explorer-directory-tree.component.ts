/* eslint-disable @angular-eslint/component-selector */
import {
  AfterContentInit,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  inject,
  Input,
  Output,
  ViewChild,
} from '@angular/core';
import { FormBuilder, FormGroup, Validators, FormsModule, ReactiveFormsModule } from '@angular/forms';
import * as DescriptorService from '../../proxy/dignite/file-explorer/directories';
import { DirectoryDescriptorDto } from '../../proxy/dignite/file-explorer/directories';
import { Confirmation, ConfirmationService, ThemeSharedModule } from '@abp/ng.theme.shared';
import { finalize, map, of, tap } from 'rxjs';
import { CoreModule, LocalizationService } from '@abp/ng.core';
import { ValidatorsService } from '../../services/validators.service';
import { CommonModule } from '@angular/common';
import { TreeModule } from '@abp/ng.components/tree';

export const MY_FILES_NODE_KEY = '__file-explorer-my-files__';
export const isMyFilesNode = (node: { key?: string } | null | undefined): boolean =>
  node?.key === MY_FILES_NODE_KEY;

/**
 * A directory, reshaped in place by {@link FileExplorerDirectoryTreeComponent.buildNodeTree} into
 * what `abp-tree` expects (`title`/`key`/`children`/...), plus the synthetic "My Files" virtual root.
 */
export interface DirectoryTreeNode extends Partial<DirectoryDescriptorDto> {
  key: string;
  title: string;
  children: DirectoryTreeNode[];
  expanded: boolean;
  isLeaf?: boolean;
  isVirtualRoot?: boolean;
  isFirstChildOfDirectory?: boolean;
}

type TreeDropEvent = {
  pos: number;
  dragNode: { key: string };
  node: {
    key: string;
    origin: { order: number; children: unknown[] };
    children?: Array<{ key: string }>;
    parentNode?: {
      key: string;
      origin: { order: number; children: unknown[] };
      children?: Array<{ key: string }>;
      parentNode?: { key: string } | null;
    } | null;
  };
};

@Component({
  selector: 'fe-file-explorer-directory-tree',
  templateUrl: './file-explorer-directory-tree.component.html',
  styleUrls: ['./file-explorer-directory-tree.component.scss'],
  imports: [CommonModule, FormsModule, ReactiveFormsModule, CoreModule, ThemeSharedModule, TreeModule],
})
export class FileExplorerDirectoryTreeComponent implements AfterContentInit {
  private readonly hostElement = inject(ElementRef<HTMLElement>);

  constructor(
    private directoryDescriptorService: DescriptorService.DirectoryDescriptorService,
    private fb: FormBuilder,
    private confirmation: ConfirmationService,
    private localizationService: LocalizationService,
  ) {}

  directoryTree: DirectoryTreeNode[] = [];

  selectedTreeNode: DirectoryTreeNode | undefined;
  @Input()
  set theSelectedTreeNode(v: DirectoryTreeNode | undefined) {
    this.selectedTreeNode = v;
  }

  nodeBeingEdited: DirectoryTreeNode | undefined;

  expandedNodeKeys: string[] = [MY_FILES_NODE_KEY];

  @Input()
  set fileContainerName(v: string) {
    if (v) {
      this._fileContainerName = v;
    }
  }
  get fileContainerName(): string {
    return this._fileContainerName;
  }
  private _fileContainerName = '';

  @Output() nodeClick = new EventEmitter<DirectoryTreeNode>();
  @Output() treeNodeData = new EventEmitter<DirectoryTreeNode[]>();

  ngAfterContentInit(): void {
    if (this.fileContainerName) {
      this.loadData();
    }
  }

  loadData(): void {
    if (this.fileContainerName) {
      this.loadDirectoryTree();
    }
  }

  private loadDirectoryTree(): void {
    this.directoryDescriptorService
      .getList({ containerName: this.fileContainerName })
      .subscribe(res => {
        const directories = this.buildNodeTree(res.items);
        this.directoryTree = [this.createMyFilesNode(directories)];
        this.treeNodeData.emit(this.directoryTree);
      });
  }

  private createMyFilesNode(children: DirectoryTreeNode[]): DirectoryTreeNode {
    const title = this.localizationService.instant('FileExplorer::MyFiles');
    return {
      id: MY_FILES_NODE_KEY,
      key: MY_FILES_NODE_KEY,
      name: title,
      title,
      parentId: null,
      children,
      expanded: this.expandedNodeKeys.includes(MY_FILES_NODE_KEY),
      isLeaf: children.length === 0,
      isVirtualRoot: true,
    };
  }

  isMyFilesNode = isMyFilesNode;

  /** Reshapes the flat, parent-referencing directory list into the nested tree `abp-tree` renders. */
  private buildNodeTree(
    items: DirectoryDescriptorDto[],
    parentId: string | null = null,
  ): DirectoryTreeNode[] {
    const result = (items as unknown as DirectoryTreeNode[])
      .filter(item => item.parentId === parentId)
      .sort((a, b) => (a.order ?? 0) - (b.order ?? 0));

    result.forEach((node, index) => {
      node.title = node.name ?? '';
      node.key = node.id ?? '';
      node.expanded = this.expandedNodeKeys.includes(node.key);
      node.isFirstChildOfDirectory = parentId !== null && index === 0;
      node.children = (node.children ?? []) as DirectoryTreeNode[];
      node.children.sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
      if (node.children.length > 0) {
        this.buildNodeTree(node.children as unknown as DirectoryDescriptorDto[], node.id);
      }
    });

    return result;
  }

  /** Dragged-directory highlight state, tracked globally so it clears even if the drop lands outside the tree. */
  isDirectoryDragging = false;

  @HostListener('document:dragstart', ['$event'])
  onDirectoryDragStart(event: DragEvent): void {
    if (event.target instanceof Node && this.hostElement.nativeElement.contains(event.target)) {
      this.isDirectoryDragging = true;
    }
  }

  @HostListener('document:dragend')
  onDirectoryDragEnd(): void {
    this.isDirectoryDragging = false;
  }

  beforeDrop = (arg: unknown) => {
    const { pos, dragNode, node } = arg as TreeDropEvent;
    if (![0, 1, -1].includes(pos) || this.isMyFilesNode(dragNode)) {
      return of(false);
    }

    // "My Files" is the virtual root; a directory can't be dropped before or after it.
    if (this.isMyFilesNode(node) && pos !== 0) {
      return of(false);
    }

    const targetParent = node.parentNode;
    // The indent guide to the right of an expanded directory is technically "before its first
    // child", but the intended drop target there is still "the sibling slot after that directory" -
    // not a second way to create a child.
    const isIndentedFirstChildGap =
      pos === -1 &&
      !!targetParent &&
      !this.isMyFilesNode(targetParent) &&
      targetParent.children?.[0]?.key === node.key;

    if (isIndentedFirstChildGap) {
      const grandParentKey = targetParent.parentNode?.key;
      const parentId =
        !grandParentKey || grandParentKey === MY_FILES_NODE_KEY ? null : grandParentKey;
      const order = targetParent.origin.order + 1;

      return this.directoryDescriptorService.move(dragNode.key, { parentId, order }).pipe(
        tap(() => this.loadDirectoryTree()),
        // Refresh from the server's own reordering instead of letting ng-zorro also apply its
        // "before the first child" move locally.
        map(() => false),
      );
    }

    const parentKey = node.parentNode?.key;
    const parentId =
      pos === 0
        ? this.isMyFilesNode(node)
          ? null
          : node.key
        : !parentKey || parentKey === MY_FILES_NODE_KEY
          ? null
          : parentKey;
    const order =
      pos === 0
        ? (node.origin.children || []).length + 1
        : pos === 1
          ? node.origin.order + 1
          : node.origin.order;

    return this.directoryDescriptorService.move(dragNode.key, { parentId, order }).pipe(
      tap(() => this.loadDirectoryTree()),
      map(() => true),
    );
  };

  /**
   * The node icon itself stops propagation via `(click)="$event.stopPropagation()"`, so a click on
   * it never reaches here - no need to distinguish where the click came from.
   */
  activeNode(node: DirectoryTreeNode): void {
    if (this.selectedTreeNode && this.selectedTreeNode.key === node.key) return;
    this.selectedTreeNode = node;
    this.nodeClick.emit(node);
  }

  isNodeSelected = (node: DirectoryTreeNode): boolean => {
    return !!this.selectedTreeNode && node.key === this.selectedTreeNode.key;
  };

  onExpandChange(event: { node: DirectoryTreeNode }): void {
    const key = event.node.key;
    this.expandedNodeKeys = this.expandedNodeKeys.includes(key)
      ? this.expandedNodeKeys.filter(k => k !== key)
      : [...this.expandedNodeKeys, key];
  }

  directoryFormModalOpen = false;
  directoryFormBusy = false;
  directoryForm: FormGroup | undefined;

  @ViewChild('directoryFormSubmit', { static: false }) directoryFormSubmit: ElementRef<HTMLButtonElement>;

  openDirectoryForm(parentNode?: DirectoryTreeNode, editing = false): void {
    this.directoryFormModalOpen = true;
    this.directoryForm = this.fb.group({
      containerName: [this.fileContainerName || '', Validators.required],
      name: ['', Validators.required],
      parentId: [
        parentNode && !this.isMyFilesNode(parentNode) ? parentNode.key : null,
      ],
    });

    if (editing && parentNode) {
      this.nodeBeingEdited = parentNode;
      this.directoryForm.patchValue({ name: parentNode.name });
    }
  }

  deleteDirectory(node: DirectoryTreeNode): void {
    this.confirmation
      .warn('FileExplorer::DeletionConfirmationMessage', 'FileExplorer::DeletionConfirmationTitle', {
        messageLocalizationParams: [node.title],
      })
      .subscribe((status: Confirmation.Status) => {
        if (status !== 'confirm') return;

        this.directoryDescriptorService.delete(node.key).subscribe(() => {
          this.directoryFormModalOpen = false;
          if (this.nodeBeingEdited && this.nodeBeingEdited.key === node.key) {
            this.nodeBeingEdited = undefined;
          }
          this.loadDirectoryTree();
        });
      });
  }

  onDirectoryFormVisibleChange(visible: boolean): void {
    if (!visible) {
      this.directoryForm = undefined;
      this.nodeBeingEdited = undefined;
    }
  }

  private readonly validatorsService = inject(ValidatorsService);

  saveDirectoryForm(): void {
    if (!this.directoryForm) return;

    const input = this.directoryForm.value;
    const validationStatus = this.validatorsService.getFormValidationStatus(this.directoryForm);
    if (this.validatorsService.isCheckForm(validationStatus, 'FileExplorer')) return;
    if (!this.directoryForm.valid || this.directoryFormBusy) return;

    this.directoryFormBusy = true;
    const request = this.nodeBeingEdited
      ? this.directoryDescriptorService.update(this.nodeBeingEdited.key, input)
      : this.directoryDescriptorService.create(input);

    request.pipe(finalize(() => (this.directoryFormBusy = false))).subscribe(() => {
      this.directoryFormModalOpen = false;
      this.onDirectoryFormVisibleChange(false);
      this.loadDirectoryTree();
    });
  }
}
