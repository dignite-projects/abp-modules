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
import { Confirmation, ConfirmationService, ThemeSharedModule } from '@abp/ng.theme.shared';
import { finalize, map, of, tap } from 'rxjs';
import { CoreModule, LocalizationService } from '@abp/ng.core';
import { ValidatorsService } from '../../services/validators.service';
import { CommonModule } from '@angular/common';
import { TreeModule, TreeNode } from '@abp/ng.components/tree';

export const MY_FILES_NODE_KEY = '__file-explorer-my-files__';
export const isMyFilesNode = (node: any): boolean => node?.key === MY_FILES_NODE_KEY;

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
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    CoreModule,
    ThemeSharedModule,
    TreeModule,
  ],
})
export class FileExplorerDirectoryTreeComponent implements AfterContentInit {
  private readonly hostElement = inject(ElementRef<HTMLElement>);

  constructor(
    private directoryDescriptorService: DescriptorService.DirectoryDescriptorService,
    private fb: FormBuilder,
    private confirmation: ConfirmationService,
    private localizationService: LocalizationService,
  ) {}

  /**文件分组列表 */
  fileGroupList: any[] = [];

  /**选择的tree节点 */
  _theSelectedTreeNode: any = '';
  @Input()
  public set theSelectedTreeNode(v: any) {
    this._theSelectedTreeNode = v;
  }

  /**正在编辑的节点 */
  theNodeBeingEdited: any = '';

  /**已展开的节点 */
  anExpandedNode: any[] = [MY_FILES_NODE_KEY];

  /**图片容器 */
  _fileContainerName: string;

  /**tree节点选择回调 */
  @Output() nodeClick = new EventEmitter();
  /**获取数据后回调给file-explorer-modal */
  @Output() treeNodeData = new EventEmitter();
  @Input()
  public set fileContainerName(v: string) {
    if (v) {
      this._fileContainerName = v;
      // this.loadData();
    }
  }
  ngAfterContentInit(): void {
    //Called after ngOnInit when the component's or directive's content has been initialized.
    //Add 'implements AfterContentInit' to the class.
    if (this._fileContainerName) {
      this.loadData();
    }
  }

  loadData() {
    if (this._fileContainerName) {
      this.getFileGroupList();
    }
  }

  /**获取文件分组 */
  getFileGroupList() {
    this.directoryDescriptorService
      .getList({
        containerName: this._fileContainerName,
      })
      .subscribe(async res => {
        const directories = await this.setTheValueOfTheNodeRecursively(res.items);
        this.fileGroupList = [this.createMyFilesNode(directories)];
        this.treeNodeData.emit(this.fileGroupList);
      });
  }

  /**创建不落库的虚拟根节点 */
  private createMyFilesNode(children: any[]): any {
    const title = this.localizationService.instant('FileExplorer::MyFiles');
    return {
      id: MY_FILES_NODE_KEY,
      key: MY_FILES_NODE_KEY,
      entity: { id: MY_FILES_NODE_KEY, name: title, parentId: null },
      name: title,
      title: title,
      parentId: null,
      order: -1,
      children,
      expanded: this.anExpandedNode.includes(MY_FILES_NODE_KEY),
      isLeaf: children.length === 0,
      isVirtualRoot: true,
    };
  }

  isMyFilesNode = isMyFilesNode;

  /**递归-将列表转化为父子级结构 */
  setTheValueOfTheNodeRecursively(array: any[], parentId: any = null, root?: any[]): any {
    const rootList = root || array;
    const result = array.filter(item => item.parentId === parentId);
    result.sort((a, b) => a.order - b.order);
    result.map((el: any, index: number) => {
      el.title = el.name;
      el.key = el.id;
      el.entity = el.id; /** 节点值 */
      el.expanded = this.anExpandedNode.includes(el.key);
      el.isFirstChildOfDirectory = parentId !== null && index === 0;
      el.children = el.children || [];
      el.children.sort((a, b) => a.order - b.order);
      if (el.children.length > 0) {
        this.setTheValueOfTheNodeRecursively(el.children, el.id, rootList);
      }
    });
    return result;
  }

  /**目录拖拽状态，用于控制树的拖拽提示并清理残留提示线 */
  isDirectoryDragging = false;

  @HostListener('document:dragstart', ['$event'])
  onDirectoryDragStart(event: DragEvent) {
    if (event.target instanceof Node && this.hostElement.nativeElement.contains(event.target)) {
      this.isDirectoryDragging = true;
    }
  }

  @HostListener('document:dragend')
  onDirectoryDragEnd() {
    this.isDirectoryDragging = false;
  }

  /**tree-拖拽 -验证*/
  beforeDrop = (arg: unknown) => {
    const { pos, dragNode, node } = arg as TreeDropEvent;
    if (![0, 1, -1].includes(pos) || this.isMyFilesNode(dragNode)) {
      return of(false);
    }

    // “我的文件”是虚拟根节点，不能把目录放到它的前后。
    if (this.isMyFilesNode(node) && pos !== 0) {
      return of(false);
    }

    const targetParent = node.parentNode;
    // 展开目录后的右侧缩进线实际是“第一条子目录之前”。产品交互要求该位置仍表示
    // “目标目录之后的同级位置”，不能作为另一种创建子目录的入口。
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
        tap(() => this.getFileGroupList()),
        // 阻止 ng-zorro 再按“第一条子目录之前”修改本地树，使用接口刷新后的结构。
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
      tap(() => this.getFileGroupList()),
      map(() => true),
    );
  };

  /**tree--选择节点。节点图标本身通过 (click)="$event.stopPropagation()" 阻止冒泡，
   * 因此点击图标不会走到这里，这里不需要再区分点击来源 */
  activeNode(node: TreeNode<any>) {
    if (this._theSelectedTreeNode?.key == node.key) return;
    this._theSelectedTreeNode = node;
    this.nodeClick.emit(node);
  }

  /**判断节点是否选中 */
  isNodeSelected = el => {
    return el.key === this._theSelectedTreeNode?.key;
  };

  /**点击展开树节点图标触发 */
  nzExpandChange(event) {
    let anExpandedNode = this.anExpandedNode;
    if (anExpandedNode.includes(event.node.key)) {
      anExpandedNode = anExpandedNode.filter(key => key !== event.node.key);
    } else {
      anExpandedNode.push(event.node.key);
    }
    this.anExpandedNode = anExpandedNode;
  }

  /**增加分组 */
  addDescriptorBtn(items: any = '', edit = false) {
    this.ModalDescriptorOpen = true;
    this.ModalDescriptorForm = this.fb.group({
      containerName: [this._fileContainerName || '', Validators.required],
      name: ['', Validators.required],
      parentId: [this.isMyFilesNode(items) ? null : items?.key || null],
    });
    /**编辑 */
    if (edit) {
      this.theNodeBeingEdited = items.origin;
      this.ModalDescriptorForm.patchValue({
        name: items.origin.name,
      });
    }
  }

  /**删除分组 */
  deleteDescriptorBtn(node) {
    this.confirmation
      .warn('FileExplorer::DeletionConfirmationMessage', 'FileExplorer::DeletionConfirmationTitle', {
        messageLocalizationParams: [node.title],
      })
      .subscribe((status: Confirmation.Status) => {
        if (status == 'confirm') {
          this.directoryDescriptorService.delete(node.key).subscribe(res => {
            this.ModalDescriptorOpen = false;
            if (this.theNodeBeingEdited.key == node.key) this.theNodeBeingEdited = '';
            this.getFileGroupList();
          });
        }
      });
  }

  /**分组 */
  /**模态框-状态-是否打开 */
  ModalDescriptorOpen = false;

  /**模态框-descriptor-繁忙状态-用于确定模态的繁忙状态是否为真 */
  ModalDescriptorBusy = false;

  /**模态框-descriptor-表单 */
  ModalDescriptorForm: FormGroup | undefined;

  /**模态框-descriptor-表单--控件模板-动态赋值表单控件 */
  @ViewChild('ModalFormDescriptorSubmit', { static: false }) ModalFormDescriptorSubmit: ElementRef;

  /**模态框-descriptor-状态改变回调 */
  ModalDescriptorVisibleChange(event) {
    if (!event) {
      this.ModalDescriptorForm = undefined;
      this.theNodeBeingEdited = '';
      return;
    }
  }
  formValidation: any = '';
  private validatorsService = inject(ValidatorsService);
  /**f分组模态框保存 */
  createOrEditSave() {
    const input = this.ModalDescriptorForm.value;
    this.formValidation = this.validatorsService.getFormValidationStatus(this.ModalDescriptorForm);
    if (this.validatorsService.isCheckForm(this.formValidation, 'FileExplorer')) return;
    if (!this.ModalDescriptorForm.valid) return;
    if (this.ModalDescriptorBusy) return;
    this.ModalDescriptorBusy = true;

    if (this.theNodeBeingEdited) {
      this.directoryDescriptorService
        .update(this.theNodeBeingEdited.key, input)
        .pipe(
          finalize(() => {
            this.ModalDescriptorBusy = false;
          })
        )
        .subscribe(res => {
          this.ModalDescriptorOpen = false;
          this.ModalDescriptorVisibleChange(false);
          this.getFileGroupList();
        });
      return;
    }
    this.directoryDescriptorService
      .create(input)
      .pipe(
        finalize(() => {
          this.ModalDescriptorBusy = false;
        })
      )
      .subscribe(res => {
        this.ModalDescriptorOpen = false;
        this.ModalDescriptorVisibleChange(false);
        this.getFileGroupList();
      });
  }
}
