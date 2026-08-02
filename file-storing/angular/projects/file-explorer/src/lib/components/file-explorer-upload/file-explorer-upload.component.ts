/* eslint-disable @angular-eslint/component-selector */
import { Component, ElementRef, EventEmitter, Input, OnDestroy, Output, ViewChild } from '@angular/core';
import { ObjectUrlService } from '../../services/object-url.service';
import { FormatFileSizePipe } from '../../pipe/format-file-size.pipe';
import { CoreModule } from '@abp/ng.core';
import { FilePreviewComponent } from '../../previews/file-preview.component';

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
  ) {}

  /**是否多选 */
  _multiple = true;
  @Input()
  public set multiple(v: boolean) {
    this._multiple = v;
    // if (v) { }
  }
  /**文件数据--已上传的数据 */
  _fileData: any[] = [];
  @Input()
  public set fileData(v: any[]) {
    this._fileData = v;
    if (v?.length > 0) {
      this.getFileChange({ target: { files: v } });
    }
  }
  /** 跟随表单提交--已提交的数据，或选择的数据源--回调*/
  @Output() fileDataChange = new EventEmitter();

  /**文件大小限制
   * @param 1mb
   */
  sizeLimit = 1048576;
  @Input()
  public set limit(v: number) {
    this.sizeLimit = v;
  }

  /**文件表格数据 */
  filesTableData: any[] = [];
  private readonly previewObjectUrls = new Set<string>();
  /** 待删除已上传的文件们*/
  deleteTheUploadedFiles: any[] = [];

  /**获取文件选择框的元素 */
  @ViewChild('fileUploadInput', { static: true }) fileUploadInput: ElementRef;

  /**获取文件信息改变 */
  async getFileChange(event) {
    const files = new Array(...event.target.files);
    await this.waitFileToAddTable(files);
    this.fileHandling();
  }

  /**等待将文件数据加入到文件表格数据中 */
  async waitFileToAddTable(files) {
    this.filesTableData.push(...(await this.setFileSizeUnits(files)));
  }

  /**删除文件表格的项 */
  deleteFileTableItem(index, item) {
    this.filesTableData.splice(index, 1);
    this.releasePreviewUrl(item.src);
    if (item.id) {
      this.deleteTheUploadedFiles.push(item);
    }
    this.fileHandling();
  }

  /**文件处理-调用回调函数 */
  fileHandling() {
    const theFilesToBeUploaded = this.filesTableData.filter(el => !el.id);
    //判断图片大小是否超过限制-用于判断表单是否允许提交
    const isSubmit = !this.filesTableData.some(el => el.size > this.sizeLimit);
    this.fileDataChange.emit({
      theFilesToBeUploaded,
      deleteTheUploadedFiles: this.deleteTheUploadedFiles,
      isSubmit,
    });
  }

  /**设置值文件大小单位/ */
  async setFileSizeUnits(files: File[] | any[]): Promise<any> {
    for (const file of files as any[]) {
      const fileItem = file as any;
      const previewItem = fileItem as { src?: string };
      fileItem.fileSize = this.formatFileSizePipe.transform(fileItem.size);
      // Use a browser-managed object URL instead of retaining a base64 copy in memory.
      if (!previewItem.src && fileItem instanceof Blob) {
        previewItem.src = this.objectUrlService.get(fileItem);
        this.previewObjectUrls.add(previewItem.src);
      }
    }

    return files;
  }

  ngOnDestroy(): void {
    for (const objectUrl of this.previewObjectUrls) {
      URL.revokeObjectURL(objectUrl);
    }
    this.previewObjectUrls.clear();
  }

  private releasePreviewUrl(url: string): void {
    if (!this.previewObjectUrls.delete(url)) {
      return;
    }

    URL.revokeObjectURL(url);
  }
}
