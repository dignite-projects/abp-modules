import {
  Component,
  EventEmitter,
  Input,
  Output,
  SimpleChanges,
  ViewChild,
  OnChanges,
} from '@angular/core';
import { DatatableComponent } from '@swimlane/ngx-datatable';
import { Confirmation, ConfirmationService, ThemeSharedModule, ToasterService } from '@abp/ng.theme.shared';
import {
  PagedResultDto,
  ABP,
  CoreModule,
  ListService,
  LocalizationService,
  LIST_QUERY_DEBOUNCE_TIME,
} from '@abp/ng.core';
import {
  FileDescriptorDto,
  FileDescriptorService,
  GetFilesInput,
} from '../../proxy/dignite/file-explorer/files';
import { FormControl, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { CommonModule } from '@angular/common';
import {
  FileExplorerDirectoryTreeComponent,
  MY_FILES_NODE_KEY,
  isMyFilesNode,
} from '../file-explorer-directory-tree/file-explorer-directory-tree.component';
import { FilePreviewComponent } from '../../previews/file-preview.component';
import { FormatFileSizePipe } from '../../pipe/format-file-size.pipe';

@Component({
  // eslint-disable-next-line @angular-eslint/component-selector
  selector: 'fe-file-explorer-modal',
  templateUrl: './file-explorer-modal.component.html',
  styleUrls: ['./file-explorer-modal.component.scss'],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CoreModule,
    ThemeSharedModule,
    FileExplorerDirectoryTreeComponent,
    FilePreviewComponent,
    FormatFileSizePipe,
  ],
  providers: [
    ListService,
    { provide: LIST_QUERY_DEBOUNCE_TIME, useValue: 500 },
  ],
})
export class FileExplorerModalComponent implements OnChanges {
  @ViewChild('fileTable') private fileTable?: DatatableComponent<FileDescriptorDto>;
  private tableRecalculationFrame?: number;

  // The file list uses the opposite visual convention for sort arrows.
  // Keep the direction sent by ListService unchanged and only swap the icons.
  readonly tableSortIcons = {
    sortAscending: 'datatable-icon-down',
    sortDescending: 'datatable-icon-up',
  };

  constructor(
    private fileService: FileDescriptorService,
    private toaster: ToasterService,
    public readonly list: ListService,
    private confirmation: ConfirmationService,
    private localizationService: LocalizationService,
  ) {}

  /**获取目录配置 */
  getFilesConfiguration(): void {
    this.fileService.getFileContainerConfiguration(this._fileContainerName).subscribe(res => {
      this.createDirectoryPermissionName = res?.createDirectoryPermissionName;
      // Reflect the container's actual server-side limit (FileSizeLimitHandler.MaxFileSize) once
      // it's known, instead of leaving the pre-fetch default of 1MB as the permanent client cap.
      if (res?.maxBlobSize > 0) {
        this.sizeLimit = res.maxBlobSize;
      }
      this.scheduleTableRecalculation();
    });
  }
  /**目录的权限名称 */
  createDirectoryPermissionName: string = null;

  /**图片容器 */
  _fileContainerName: string;
  @Input()
  public set fileContainerName(v: string) {
    if (v) {
      this._fileContainerName = v;
    }
  }

  /**是否多选 */
  _multiple = false;
  @Input()
  public set multiple(v: boolean) {
    this._multiple = v;
  }

  /**文件大小限制
   * @param 1mb
   */
  sizeLimit = 1048576;
  @Input()
  public set limit(v: number) {
    this.sizeLimit = v;
  }
  /**父组件传递的模态框状态 */
  @Input()
  public set visible(v: boolean) {
    this.ModalOpen = v;
    if (v) {
      this.loadData();
    }
  }

  /**模态框状态回调 */
  @Output() visibleChange = new EventEmitter();

  /**模态框-状态-是否打开 */
  ModalOpen = false;

  /**文件名编辑模态框状态 */
  FileNameModalOpen = false;

  /**文件名编辑模态框状态改变回调 */
  FileNameModalVisibleChange(event: boolean) {
    this.FileNameModalOpen = event;
    if (!event) {
      this.FileNameForm = undefined;
      this.newEditRow = '';
    }
  }

  /**模态框-状态改变回调 */
  ModalVisibleChange(event) {
    if (!event) {
      this.ModalOpen = false;
      this.visibleChange.emit(event);
      this.createDirectoryPermissionName = '';
      this._theSelectedTreeNode = '';
      this.selectedTable = [];
      this.uploadPictureStatusList = [];
      this.onCancelFileName();
      return;
    }
  }

  /** 模态框内容完成布局后重新计算表格列宽，避免缓存到过渡阶段的宽度。 */
  onModalInit(): void {
    this.scheduleTableRecalculation();
  }

  private scheduleTableRecalculation(): void {
    if (!this.ModalOpen) return;

    if (typeof requestAnimationFrame === 'undefined') {
      setTimeout(() => this.fileTable?.recalculate());
      return;
    }

    if (this.tableRecalculationFrame !== undefined) {
      cancelAnimationFrame(this.tableRecalculationFrame);
    }

    this.tableRecalculationFrame = requestAnimationFrame(() => {
      this.tableRecalculationFrame = requestAnimationFrame(() => {
        this.tableRecalculationFrame = undefined;
        if (this.ModalOpen) {
          this.fileTable?.recalculate();
        }
      });
    });
  }

  /**模态框保存 */
  modalSave() {
    if (this.selectedTable.length === 0) return;

    const selectedTablearr = structuredClone(this.selectedTable);
    this.selectFilefn.emit(selectedTablearr);
    this.ModalVisibleChange(false);
  }
  /**dignite-file-explorer-directory-tree */
  /**选择的tree节点 */
  _theSelectedTreeNode: any = '';
  isCreateList = false;
  /**初始化数据 */
  loadData() {
    if (this.ModalOpen && this._fileContainerName) {
      this.list.maxResultCount = 50;
      this.getFilesConfiguration();
      if (!this.isCreateList) {
        this.hookToQuery();
        this.isCreateList = true;
      } else {
        this.list.get();
      }
    }
  }
  /** 从tree获取来的数据 */
  fileGroupList: any[] = [];

  /**虚拟根节点“我的文件”不对应后端目录，查询时表示全部文件 */
  private getSelectedDirectoryId(): string | undefined {
    const key = this._theSelectedTreeNode?.key;
    return key && key !== MY_FILES_NODE_KEY ? key : undefined;
  }

  /** 从tree获取数据 */
  treeNodeData(event) {
    this.fileGroupList = this.flattenNestedArray(event);
  }

  /**获取当前目录及其所有父级目录 */
  getDirectoryPath(node: any): any[] {
    const path = [];
    const visitedKeys = new Set();
    let currentKey = node?.key;

    while (currentKey && !visitedKeys.has(currentKey)) {
      visitedKeys.add(currentKey);
      const currentNode = this.fileGroupList.find(item => item.key === currentKey || item.id === currentKey);

      if (!currentNode) break;

      path.unshift(currentNode);
      currentKey = currentNode.parentId;
    }

    return path;
  }

  /**获取文件所在目录的完整路径 */
  getFileDirectoryPath(directoryId: string): string {
    if (!directoryId) return '';

    return this.getDirectoryPath({ key: directoryId })
      .map(node => node.name || node.title)
      .filter(Boolean)
      .join(' / ');
  }
  /**
   * 将嵌套数组扁平化
   * @param {Array} nestedArray - 包含嵌套children的数组
   * @returns {Array} - 扁平化后的数组
   */
  flattenNestedArray(nestedArray) {
    const result = [];

    function flatten(items) {
      if (!items) return;

      for (const item of items) {
        // 将当前项添加到结果数组
        result.push({ ...item });

        // 如果有children属性且是数组，递归处理
        if (item.children && Array.isArray(item.children)) {
          flatten(item.children);
        }
      }
    }

    flatten(nestedArray);
    return result;
  }

  /**tree-节点选择 */
  _nodeClick(event) {
    this.filters.skipCount = 0;
    this._theSelectedTreeNode = event;
    this.list.get();
  }

  /**图片上传-要上传图片的状态文件列表 */
  uploadPictureStatusList: any[] = [];

  /**图片上传-获取文件信息改变 */
  async getFileChange(event) {
    const files = new Array(...event.target.files);
    this.uploadPictureStatusList = files;
    // Uploaded one at a time: concurrent creates against the same container can race inside
    // the blob storage provider (e.g. Volo.Abp.BlobStoring.Database) and surface as a spurious
    // AbpDbConcurrencyException (409) on one of the files. Sequential requests avoid that race.
    for (const file of files) {
      if (file.size > this.sizeLimit) {
        this.setUploadPictureStatus(file, 2);
        continue;
      }
      try {
        await this.uploadingFile(file);
        this.setUploadPictureStatus(file, 1);
      } catch {
        this.setUploadPictureStatus(file, 2);
      }
    }
    this.list.get();
    const isSubmit = !this.uploadPictureStatusList.some(el => el.status == 2);
    if (isSubmit) {
      setTimeout(() => {
        this.uploadPictureStatusList = [];
      }, 4000);
    }
  }

  /**图片上传-设置uploadPictureStatusList的状态 */
  setUploadPictureStatus(file, type) {
    this.uploadPictureStatusList.forEach(el => {
      if (el == file) el.status = type;
    });
  }

  /**图片上传-递归按顺序上传 */
  uploadingFile(file) {
    return new Promise((resolve, rejects) => {
      const formData = new FormData();
      formData.append('file', file, file.name);
      this.fileService.create({
        file: formData as any,
        containerName: this._fileContainerName,
        directoryId: this.getSelectedDirectoryId() ?? null,
        entityId: '',
      }).subscribe(
        res => {
          resolve({ file: file.name, status: 'success', response: res });
        },
        err => {
          rejects({ file: file.name, status: 'fail', error: err });
        },
      );
    });
  }

  /**文件表格-数据*/
  data: PagedResultDto<FileDescriptorDto> = {
    items: [],
    totalCount: 0,
  };

  /**文件表格-条件*/
  filters = {} as GetFilesInput;

  /**选择文件回调 */
  @Output() selectFilefn = new EventEmitter<any[]>();

  /**文件表格-获取表格数据 */
  hookToQuery() {
    const getData = (query: ABP.PageQueryParams) =>
      this.fileService.getList({
        ...query,
        ...this.filters,
        containerName: this._fileContainerName,
        directoryId: this.getSelectedDirectoryId(),
      });
    const setData = (list: PagedResultDto<FileDescriptorDto>) => {
      this.data = list;
      this.selectedTable = [];
      this.isAllSelected = false;
      this.scheduleTableRecalculation();
    };
    this.list.hookToQuery(getData).subscribe(setData);
  }

  /**删除所有选中图片 */
  onDeleteAllSelectFile() {
    this.confirmation
      .warn('FileExplorer::BatchDeletionConfirmationMessage', 'FileExplorer::BatchDeletionConfirmationTitle', {
        messageLocalizationParams: [String(this.selectedTable.length)],
      })
      .subscribe(async (status: Confirmation.Status) => {
        if (status == 'confirm') {
          const selectedTable = this.selectedTable;
          const result = await this.batchDeleteItems(selectedTable);
          if (result.success) {
            this.toaster.success(result.message);
          } else {
            this.toaster.error(result.message);
          }
          this.list.get();
        }
      });
  }

  /**
   * 批量删除表格项
   * @param selectedTable 需要删除的表格项数组
   * @returns 包含成功状态和失败项的结果对象
   */
  async batchDeleteItems(selectedTable: any[]) {
    // 存储所有删除请求的Promise
    const deletePromises = selectedTable.map(item => {
      return new Promise((resolve, reject) => {
        this.fileService.delete(item.id).subscribe(
          () => {
            resolve(null);
          },
          () => {
            reject(item);
          },
        );
      });
    });

    // 等待所有请求完成
    const results = await Promise.allSettled(deletePromises);
    // 收集失败的项
    const failedItems: any[] = [];
    results.forEach(result => {
      if (result.status === 'rejected') {
        failedItems.push(result.reason);
      }
    });

    return {
      success: failedItems.length === 0,
      failedItems,
      message:
        failedItems.length === 0
          ? this.localizationService.instant(`FileExplorer::DeletedSuccessfully`)
          : `${failedItems.length}个项删除失败`,
    };
  }

  /**移动文件模态框状态 */
  MoveModalOpen = false;
  MoveModalBusy = false;
  moveTargetDirectoryNode: any = '';

  openMoveModal() {
    if (this.selectedTable.length === 0 || !this.createDirectoryPermissionName) return;

    this.moveTargetDirectoryNode = '';
    this.MoveModalOpen = true;
  }

  onMoveTargetDirectoryChange(node: any) {
    this.moveTargetDirectoryNode = node;
  }

  MoveModalVisibleChange(event: boolean) {
    this.MoveModalOpen = event;
    if (!event) {
      this.MoveModalBusy = false;
      this.moveTargetDirectoryNode = '';
    }
  }

  async moveSelectedFiles() {
    const targetNode = this.moveTargetDirectoryNode;
    const targetDirectoryId = isMyFilesNode(targetNode) ? null : targetNode?.key;
    if (!targetNode?.key || this.selectedTable.length === 0 || this.MoveModalBusy) return;

    this.MoveModalBusy = true;
    const results = await Promise.allSettled(
      this.selectedTable.map(
        file =>
          new Promise((resolve, reject) => {
            this.fileService
              .update(file.id, { directoryId: targetDirectoryId })
              .subscribe({ next: resolve, error: reject });
          }),
      ),
    );
    this.MoveModalBusy = false;

    const failedCount = results.filter(result => result.status === 'rejected').length;
    if (failedCount > 0) {
      this.toaster.error(this.localizationService.instant('FileExplorer::MoveFailed'));
      return;
    }

    this.toaster.success(this.localizationService.instant('FileExplorer::MovedSuccessfully'));
    this.selectedTable = [];
    this.isAllSelected = false;
    this.list.get();
    this.MoveModalOpen = false;
  }


  /**关闭文件状态弹窗 */
  closeFileStatusModal() {
    this.uploadPictureStatusList = [];
  }

  /**文件表格-选择的表格数据项 */
  selectedTable = [];
  /**是否全选 */
  isAllSelected = false;

  /**已选定的文件 */
  @Input() selectPickerFile: any[];

  ngOnChanges(changes: SimpleChanges): void {
    const selectPickerFileChange = changes.selectPickerFile;
    if (!selectPickerFileChange) {
      return;
    }

    this.selectedTable = structuredClone(selectPickerFileChange.currentValue ?? []);
  }
  /**行选择框改变 */
  onCheckboxChangeFn(event, row, array: any[]) {
    const { checked } = event.target;
    let selectedTableArray = [...this.selectedTable];
    if (this._multiple) {
      if (checked) {
        selectedTableArray.push(row);
      } else {
        selectedTableArray = selectedTableArray.filter(el => el.id != row.id);
      }
      this.isAllSelected = this.isAllSelectedFn(array, selectedTableArray);
    } else {
      selectedTableArray.length = 0;
      selectedTableArray = checked ? [row] : [];
    }
    this.selectedTable = this.removeDuplicatesById(selectedTableArray);
  }
  /**如果selectedTableArray不含array中的所有项，则将isAllSelected设为true,否则设为false */
  isAllSelectedFn(tolalArray: any[], selectedArray: any[] = []) {
    if (tolalArray.length == 0) return false;
    return tolalArray.every(item => selectedArray.some(el => el.id === item.id));
  }
  /**选择当前页全部 */
  onSelectAllFn(event: any, array: any[]) {
    let selectedTableArray = this.selectedTable;
    if (event.target.checked) {
      selectedTableArray = this.removeDuplicatesById([...selectedTableArray, ...array]);
    } else {
      selectedTableArray = selectedTableArray.filter(el => !array.some(item => item.id === el.id));
    }
    this.isAllSelected = event.target.checked;
    this.selectedTable = selectedTableArray;
  }

  /**判断row是否选中 */
  selectedcheckbox = id => {
    return this.selectedTable.some(el => el.id === id);
  };
  /**删除数组中重复的项 */
  removeDuplicatesById(array) {
    const seenIds = {};
    return array.filter(item => {
      if (!seenIds[item.id]) {
        seenIds[item.id] = true;
        return true;
      }
      return false;
    });
  }
  /**用于编辑的表单，同时只能显示编辑一个 */
  FileNameForm: FormGroup | any;
  /**当前编辑的row */
  newEditRow: any = '';
  /**是否正在加载 */
  isloading = false;
  /**提交FileName编辑 */
  onSubmitFileName() {
    const input = this.FileNameForm.value;
    if (!this.FileNameForm.valid) return;
    if (this.isloading) return;
    this.isloading = true;
    this.fileService
      .update(input.id, {
        name: input.fileName,
      })
      .pipe(
        finalize(() => {
          this.isloading = false;
        }),
      )
      .subscribe(res => {
        //通过当前newEditRow的id,修改data.items中对应项的name
        for (const element of this.data.items) {
          if (element.id == this.newEditRow.id) {
            element.name = input.fileName;
            break;
          }
        }

        this.FileNameForm = undefined;
        this.newEditRow = '';
        this.FileNameModalOpen = false;
        this.toaster.success(this.localizationService.instant(`FileExplorer::SavedSuccessfully`));
      });
  }
  /**打开编辑 */
  onEditFileName(row) {
    this.FileNameForm = new FormGroup({
      fileName: new FormControl('', [Validators.required]),
      id: new FormControl('', [Validators.required]),
    });
    this.FileNameForm.patchValue({
      fileName: row.name,
      id: row.id,
    });
    this.newEditRow = row;
    this.FileNameModalOpen = true;
  }
  /**关闭编辑 */
  onCancelFileName() {
    this.FileNameModalOpen = false;
    this.newEditRow = '';
    this.FileNameForm = undefined;
  }
}
