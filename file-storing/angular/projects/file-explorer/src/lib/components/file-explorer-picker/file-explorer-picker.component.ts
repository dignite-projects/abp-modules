/* eslint-disable @angular-eslint/component-selector */
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
import { CoreModule } from '@abp/ng.core';
import { FileExplorerModalComponent } from '../file-explorer-modal/file-explorer-modal.component';
import { FormatFileSizePipe } from '../../pipe/format-file-size.pipe';
import { FilePreviewComponent } from '../../previews/file-preview.component';

@Component({
  
  selector: 'fe-file-explorer-picker',
  templateUrl: './file-explorer-picker.component.html',
  styleUrls: ['./file-explorer-picker.component.scss'],
  imports: [
    CoreModule,
    DragDropModule,
    FileExplorerModalComponent,
    FilePreviewComponent,
    FormatFileSizePipe,
  ],
})
export class FileExplorerPickerComponent implements OnChanges {

  /**是否多选 */
  _multiple = false
  @Input()
  public set multiple(v: boolean) {
    this._multiple = v;
  }

  /**图片容器 */
  _fileContainerName = 'Images'
  @Input()
  public set fileContainerName(v: string) {
    this._fileContainerName = v;
  }

  /**已选定的文件 */
  @Input() selectFormFile: any[]

  ngOnChanges(changes: SimpleChanges): void {
    const selectFormFileChange = changes.selectFormFile;
    if (selectFormFileChange?.currentValue?.length > 0) {
      this._fileShowTable = selectFormFileChange.currentValue
    }
  }

  @Output() selectedFileChange = new EventEmitter()

  /** 本组件的唯一数据源；也直接传给弹窗的 selectPickerFile，用于回显已选中项 */
  _fileShowTable: any[] = []

  /**表格选择的文件回调 */
  _selectFilefn(event: any[]) {
    const fileShowTable = structuredClone(event)
    this._fileShowTable = fileShowTable
    this.selectedFileChange.emit(fileShowTable)
  }

  /**模态框-状态-是否打开 */
  ModalOpen= false

  /**删除文件表格项 */
  deleteFileTableItem(i: number) {
    this._fileShowTable.splice(i, 1)
    this.selectedFileChange.emit(this._fileShowTable)
  }
  /**调整表格位置 */
  drop(event: any) {
    moveItemInArray(
      this._fileShowTable,
      event.previousIndex,
      event.currentIndex
    );
    this.selectedFileChange.emit(this._fileShowTable)
  }
}
