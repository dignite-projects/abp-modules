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
  CreateFileInput,
  FileDescriptorDto,
  FileDescriptorService,
  GetFilesInput,
} from '../../proxy/dignite/file-explorer/files';
import { FormControl, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { CommonModule } from '@angular/common';
import {
  DirectoryTreeNode,
  FileExplorerDirectoryTreeComponent,
  MY_FILES_NODE_KEY,
  isMyFilesNode,
} from '../file-explorer-directory-tree/file-explorer-directory-tree.component';
import { FilePreviewComponent } from '../../previews/file-preview.component';
import { FormatFileSizePipe } from '../../pipe/format-file-size.pipe';

/** A file mid-upload, tracked alongside its outcome for the status toast. */
interface UploadingFile extends File {
  status?: 1 | 2; // 1 = succeeded, 2 = failed (over the size limit, or the request itself failed)
}

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

  private loadContainerConfiguration(): void {
    this.fileService.getFileContainerConfiguration(this.fileContainerName).subscribe(res => {
      this.createDirectoryPermissionName = res?.createDirectoryPermissionName;
      // Reflect the container's actual server-side limit (FileSizeLimitHandler.MaxFileSize) once
      // it's known, instead of leaving the pre-fetch default of 1MB as the permanent client cap.
      if (res?.maxBlobSize > 0) {
        this.sizeLimit = res.maxBlobSize;
      }
      this.scheduleTableRecalculation();
    });
  }

  createDirectoryPermissionName = '';

  /** No default: an unconfigured container is a caller bug, not implicitly "Images". */
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

  @Input() multiple = false;

  sizeLimit = 1048576;
  @Input()
  set limit(v: number) {
    this.sizeLimit = v;
  }

  @Input()
  set visible(v: boolean) {
    this.modalOpen = v;
    if (v) {
      this.loadData();
    }
  }

  @Output() visibleChange = new EventEmitter<boolean>();

  modalOpen = false;

  fileNameModalOpen = false;

  onFileNameModalVisibleChange(visible: boolean): void {
    this.fileNameModalOpen = visible;
    if (!visible) {
      this.fileNameForm = undefined;
      this.editingFileRow = undefined;
    }
  }

  onModalVisibleChange(visible: boolean): void {
    if (visible) return;

    this.modalOpen = false;
    this.visibleChange.emit(visible);
    this.createDirectoryPermissionName = '';
    this.selectedTreeNode = undefined;
    this.selectedFiles = [];
    this.uploadingFiles = [];
    this.onCancelFileNameEdit();
  }

  /** Recomputes the table's column widths once the modal has finished its open transition. */
  onModalInit(): void {
    this.scheduleTableRecalculation();
  }

  private scheduleTableRecalculation(): void {
    if (!this.modalOpen) return;

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
        if (this.modalOpen) {
          this.fileTable?.recalculate();
        }
      });
    });
  }

  modalSave(): void {
    if (this.selectedFiles.length === 0) return;

    const selected = structuredClone(this.selectedFiles);
    this.selectFilefn.emit(selected);
    this.onModalVisibleChange(false);
  }

  selectedTreeNode: DirectoryTreeNode | undefined;
  private hasQueryHook = false;

  loadData(): void {
    if (this.modalOpen && this.fileContainerName) {
      this.list.maxResultCount = 50;
      this.loadContainerConfiguration();
      if (!this.hasQueryHook) {
        this.hookToQuery();
        this.hasQueryHook = true;
      } else {
        this.list.get();
      }
    }
  }

  /** Flattened directory list from the tree, for the breadcrumb-style path lookups below. */
  private flattenedDirectories: DirectoryTreeNode[] = [];

  private getSelectedDirectoryId(): string | undefined {
    const key = this.selectedTreeNode?.key;
    return key && key !== MY_FILES_NODE_KEY ? key : undefined;
  }

  onTreeNodeData(nodes: DirectoryTreeNode[]): void {
    this.flattenedDirectories = this.flattenTree(nodes);
  }

  private getDirectoryPath(node: { key?: string } | undefined): DirectoryTreeNode[] {
    const path: DirectoryTreeNode[] = [];
    const visitedKeys = new Set<string>();
    let currentKey = node?.key;

    while (currentKey && !visitedKeys.has(currentKey)) {
      visitedKeys.add(currentKey);
      const currentNode = this.flattenedDirectories.find(item => item.key === currentKey);

      if (!currentNode) break;

      path.unshift(currentNode);
      currentKey = currentNode.parentId ?? undefined;
    }

    return path;
  }

  getFileDirectoryPath(directoryId: string | undefined): string {
    if (!directoryId) return '';

    return this.getDirectoryPath({ key: directoryId })
      .map(node => node.name || node.title)
      .filter(Boolean)
      .join(' / ');
  }

  private flattenTree(nodes: DirectoryTreeNode[]): DirectoryTreeNode[] {
    const result: DirectoryTreeNode[] = [];

    const visit = (items: DirectoryTreeNode[]) => {
      for (const item of items) {
        result.push(item);
        if (item.children?.length) {
          visit(item.children);
        }
      }
    };

    visit(nodes);
    return result;
  }

  onTreeNodeClick(node: DirectoryTreeNode): void {
    this.filters.skipCount = 0;
    this.selectedTreeNode = node;
    this.list.get();
  }

  uploadingFiles: UploadingFile[] = [];

  async onFileInputChange(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []) as UploadingFile[];
    this.uploadingFiles = files;
    // Uploaded one at a time: concurrent creates against the same container can race inside
    // the blob storage provider (e.g. Volo.Abp.BlobStoring.Database) and surface as a spurious
    // AbpDbConcurrencyException (409) on one of the files. Sequential requests avoid that race.
    for (const file of files) {
      if (file.size > this.sizeLimit) {
        this.setUploadStatus(file, 2);
        continue;
      }
      try {
        await this.uploadFile(file);
        this.setUploadStatus(file, 1);
      } catch {
        this.setUploadStatus(file, 2);
      }
    }
    this.list.get();
    const allSucceeded = !this.uploadingFiles.some(file => file.status === 2);
    if (allSucceeded) {
      setTimeout(() => {
        this.uploadingFiles = [];
      }, 4000);
    }
  }

  private setUploadStatus(file: UploadingFile, status: UploadingFile['status']): void {
    for (const uploading of this.uploadingFiles) {
      if (uploading === file) uploading.status = status;
    }
  }

  private uploadFile(file: File): Promise<FileDescriptorDto> {
    return new Promise((resolve, reject) => {
      const formData = new FormData();
      formData.append('file', file, file.name);
      this.fileService
        .create({
          file: formData as unknown as CreateFileInput['file'],
          containerName: this.fileContainerName,
          directoryId: this.getSelectedDirectoryId() ?? null,
          entityId: '',
        })
        .subscribe({ next: resolve, error: reject });
    });
  }

  data: PagedResultDto<FileDescriptorDto> = {
    items: [],
    totalCount: 0,
  };

  filters = {} as GetFilesInput;

  @Output() selectFilefn = new EventEmitter<FileDescriptorDto[]>();

  private hookToQuery(): void {
    const getData = (query: ABP.PageQueryParams) =>
      this.fileService.getList({
        ...query,
        ...this.filters,
        containerName: this.fileContainerName,
        directoryId: this.getSelectedDirectoryId(),
      });
    const setData = (list: PagedResultDto<FileDescriptorDto>) => {
      this.data = list;
      this.selectedFiles = [];
      this.isAllSelected = false;
      this.scheduleTableRecalculation();
    };
    this.list.hookToQuery(getData).subscribe(setData);
  }

  deleteSelectedFiles(): void {
    this.confirmation
      .warn('FileExplorer::BatchDeletionConfirmationMessage', 'FileExplorer::BatchDeletionConfirmationTitle', {
        messageLocalizationParams: [String(this.selectedFiles.length)],
      })
      .subscribe(async (status: Confirmation.Status) => {
        if (status !== 'confirm') return;

        const result = await this.batchDeleteFiles(this.selectedFiles);
        if (result.success) {
          this.toaster.success(result.message);
        } else {
          this.toaster.error(result.message);
        }
        this.list.get();
      });
  }

  private async batchDeleteFiles(
    files: FileDescriptorDto[],
  ): Promise<{ success: boolean; message: string }> {
    const results = await Promise.allSettled(
      files.map(
        file =>
          new Promise<void>((resolve, reject) => {
            this.fileService.delete(file.id).subscribe({ next: () => resolve(), error: () => reject(file) });
          }),
      ),
    );

    const failedCount = results.filter(result => result.status === 'rejected').length;

    return {
      success: failedCount === 0,
      message:
        failedCount === 0
          ? this.localizationService.instant('FileExplorer::DeletedSuccessfully')
          : this.localizationService.instant('FileExplorer::ItemsFailedToDelete', String(failedCount)),
    };
  }

  moveModalOpen = false;
  moveModalBusy = false;
  moveTargetDirectoryNode: DirectoryTreeNode | undefined;

  openMoveModal(): void {
    if (this.selectedFiles.length === 0 || !this.createDirectoryPermissionName) return;

    this.moveTargetDirectoryNode = undefined;
    this.moveModalOpen = true;
  }

  onMoveTargetDirectoryChange(node: DirectoryTreeNode): void {
    this.moveTargetDirectoryNode = node;
  }

  onMoveModalVisibleChange(visible: boolean): void {
    this.moveModalOpen = visible;
    if (!visible) {
      this.moveModalBusy = false;
      this.moveTargetDirectoryNode = undefined;
    }
  }

  async moveSelectedFiles(): Promise<void> {
    const targetNode = this.moveTargetDirectoryNode;
    if (!targetNode?.key || this.selectedFiles.length === 0 || this.moveModalBusy) return;

    const targetDirectoryId = isMyFilesNode(targetNode) ? null : targetNode.key;

    this.moveModalBusy = true;
    const results = await Promise.allSettled(
      this.selectedFiles.map(
        file =>
          new Promise<void>((resolve, reject) => {
            this.fileService
              .update(file.id, { directoryId: targetDirectoryId })
              .subscribe({ next: () => resolve(), error: reject });
          }),
      ),
    );
    this.moveModalBusy = false;

    const failedCount = results.filter(result => result.status === 'rejected').length;
    if (failedCount > 0) {
      this.toaster.error(this.localizationService.instant('FileExplorer::MoveFailed'));
      return;
    }

    this.toaster.success(this.localizationService.instant('FileExplorer::MovedSuccessfully'));
    this.selectedFiles = [];
    this.isAllSelected = false;
    this.list.get();
    this.moveModalOpen = false;
  }

  closeFileStatusModal(): void {
    this.uploadingFiles = [];
  }

  selectedFiles: FileDescriptorDto[] = [];
  isAllSelected = false;

  @Input() selectPickerFile: FileDescriptorDto[];

  ngOnChanges(changes: SimpleChanges): void {
    const selectPickerFileChange = changes['selectPickerFile'];
    if (!selectPickerFileChange) {
      return;
    }

    this.selectedFiles = structuredClone(selectPickerFileChange.currentValue ?? []);
  }

  onRowCheckboxChange(event: Event, row: FileDescriptorDto, rows: FileDescriptorDto[]): void {
    const checked = (event.target as HTMLInputElement).checked;
    let selected = [...this.selectedFiles];
    if (this.multiple) {
      if (checked) {
        selected.push(row);
      } else {
        selected = selected.filter(item => item.id !== row.id);
      }
      this.isAllSelected = this.areAllSelected(rows, selected);
    } else {
      selected = checked ? [row] : [];
    }
    this.selectedFiles = this.removeDuplicatesById(selected);
  }

  private areAllSelected(rows: FileDescriptorDto[], selected: FileDescriptorDto[] = []): boolean {
    if (rows.length === 0) return false;
    return rows.every(row => selected.some(item => item.id === row.id));
  }

  onSelectAllChange(event: Event, rows: FileDescriptorDto[]): void {
    const checked = (event.target as HTMLInputElement).checked;
    this.selectedFiles = checked
      ? this.removeDuplicatesById([...this.selectedFiles, ...rows])
      : this.selectedFiles.filter(item => !rows.some(row => row.id === item.id));
    this.isAllSelected = checked;
  }

  isRowSelected = (id: string): boolean => {
    return this.selectedFiles.some(item => item.id === id);
  };

  private removeDuplicatesById(files: FileDescriptorDto[]): FileDescriptorDto[] {
    const seenIds = new Set<string>();
    return files.filter(file => {
      if (seenIds.has(file.id)) return false;
      seenIds.add(file.id);
      return true;
    });
  }

  /** Editing form, one row at a time - opening a second one replaces it. */
  fileNameForm: FormGroup | undefined;
  editingFileRow: FileDescriptorDto | undefined;
  isRenaming = false;

  onSubmitFileName(): void {
    if (!this.fileNameForm?.valid || this.isRenaming) return;

    const input = this.fileNameForm.value;
    this.isRenaming = true;
    this.fileService
      .update(input.id, { name: input.fileName })
      .pipe(finalize(() => (this.isRenaming = false)))
      .subscribe(() => {
        const row = this.data.items.find(item => item.id === this.editingFileRow?.id);
        if (row) {
          row.name = input.fileName;
        }

        this.fileNameForm = undefined;
        this.editingFileRow = undefined;
        this.fileNameModalOpen = false;
        this.toaster.success(this.localizationService.instant('FileExplorer::SavedSuccessfully'));
      });
  }

  onEditFileName(row: FileDescriptorDto): void {
    this.fileNameForm = new FormGroup({
      fileName: new FormControl(row.name, [Validators.required]),
      id: new FormControl(row.id, [Validators.required]),
    });
    this.editingFileRow = row;
    this.fileNameModalOpen = true;
  }

  onCancelFileNameEdit(): void {
    this.fileNameModalOpen = false;
    this.editingFileRow = undefined;
    this.fileNameForm = undefined;
  }
}
