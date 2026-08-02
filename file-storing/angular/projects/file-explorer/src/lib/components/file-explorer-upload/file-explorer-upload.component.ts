/* eslint-disable @angular-eslint/component-selector */
import { Component, ElementRef, EventEmitter, Input, OnDestroy, Output, ViewChild } from '@angular/core';
import { ObjectUrlService } from '../../services/object-url.service';
import { FormatFileSizePipe } from '../../pipe/format-file-size.pipe';
import { CoreModule } from '@abp/ng.core';
import { FilePreviewComponent } from '../../previews/file-preview.component';
import { FileDescriptorService } from '../../proxy/dignite/file-explorer/files';

/** A locally-picked `File` awaiting upload, or an already-uploaded file passed in via `fileData`. */
interface UploadTableItem {
  id?: string;
  name?: string;
  size: number;
  type?: string;
  fileSize?: string;
  src?: string;
}

export interface FileUploadChangeEvent {
  theFilesToBeUploaded: UploadTableItem[];
  deleteTheUploadedFiles: UploadTableItem[];
  isSubmit: boolean;
}

@Component({
  selector: 'fe-file-explorer-upload',
  templateUrl: './file-explorer-upload.component.html',
  imports: [CoreModule, FilePreviewComponent, FormatFileSizePipe],
  providers: [FormatFileSizePipe],
})
export class FileExplorerUploadComponent implements OnDestroy {
  constructor(
    private formatFileSizePipe: FormatFileSizePipe,
    private objectUrlService: ObjectUrlService,
    private fileDescriptorService: FileDescriptorService,
  ) {}

  /**
   * No default: an unconfigured container is not silently treated as any particular one - the
   * upload input just doesn't render until a container is set (see the template).
   */
  @Input()
  set fileContainerName(v: string) {
    this._fileContainerName = v ?? '';

    if (!v) return;
    // Reflects the container's actual server-side limit once known, replacing the pre-fetch
    // default of 1MB below.
    this.fileDescriptorService.getFileContainerConfiguration(v).subscribe(res => {
      if (res?.maxBlobSize > 0) {
        this.sizeLimit = res.maxBlobSize;
      }
    });
  }
  get fileContainerName(): string {
    return this._fileContainerName;
  }
  private _fileContainerName = '';

  @Input() multiple = true;

  /** Already-uploaded files to show pre-populated in the table, e.g. when editing an existing record. */
  @Input()
  set fileData(files: UploadTableItem[]) {
    if (files?.length > 0) {
      void this.addFiles(files);
    }
  }

  @Output() fileDataChange = new EventEmitter<FileUploadChangeEvent>();

  sizeLimit = 1048576;
  @Input()
  set limit(v: number) {
    this.sizeLimit = v;
  }

  filesTableData: UploadTableItem[] = [];
  private readonly previewObjectUrls = new Set<string>();
  private readonly deletedUploadedFiles: UploadTableItem[] = [];

  @ViewChild('fileUploadInput', { static: true }) fileUploadInput: ElementRef<HTMLInputElement>;

  async onFileInputChange(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    await this.addFiles(Array.from(input.files ?? []));
    this.emitChange();
  }

  deleteFileTableItem(index: number, item: UploadTableItem): void {
    this.filesTableData.splice(index, 1);
    this.releasePreviewUrl(item.src);
    if (item.id) {
      this.deletedUploadedFiles.push(item);
    }
    this.emitChange();
  }

  private async addFiles(files: UploadTableItem[]): Promise<void> {
    for (const file of files) {
      file.fileSize = this.formatFileSizePipe.transform(file.size);
      // Use a browser-managed object URL instead of retaining a base64 copy in memory.
      if (!file.src && file instanceof Blob) {
        const objectUrl = this.objectUrlService.get(file);
        (file as UploadTableItem).src = objectUrl;
        this.previewObjectUrls.add(objectUrl);
      }
    }

    this.filesTableData.push(...files);
  }

  private emitChange(): void {
    const theFilesToBeUploaded = this.filesTableData.filter(item => !item.id);
    const isSubmit = !this.filesTableData.some(item => item.size > this.sizeLimit);
    this.fileDataChange.emit({
      theFilesToBeUploaded,
      deleteTheUploadedFiles: this.deletedUploadedFiles,
      isSubmit,
    });
  }

  ngOnDestroy(): void {
    for (const objectUrl of this.previewObjectUrls) {
      URL.revokeObjectURL(objectUrl);
    }
    this.previewObjectUrls.clear();
  }

  private releasePreviewUrl(url?: string): void {
    if (!url || !this.previewObjectUrls.delete(url)) {
      return;
    }

    URL.revokeObjectURL(url);
  }
}
