import { Component } from '@angular/core';
import { FileExplorerUploadComponent, FileExplorerPickerComponent } from '@dignite/ng.file-explorer';

@Component({
  
  // eslint-disable-next-line @angular-eslint/component-selector
  selector: 'app-file-upload-demo',
  templateUrl: './file-upload-demo.component.html',
  styleUrls: ['./file-upload-demo.component.scss'],
  imports: [FileExplorerUploadComponent, FileExplorerPickerComponent],
})
export class FileUploadDemoComponent {

  /**跟随表单提交--已提交的数据，或选择的数据源 */
  fileSubmittedData: any[] = []

  /**跟随表单提交--待提交的数据
   * 
   * @param 待上传的文件们
   * @param 待删除已上传的文件们
   */
  fileDataToBeSubmitted: any

  /**跟随表单提交--数据发生改变回调 */
  fileDataChange(event) {
    this.fileDataToBeSubmitted = event
  }

  /**选择文件-弹窗的-已选定的文件 */
  selectedFileGroup:any[]=[]

  /**_selectedFile改变回调 */
  _selectedFileChange(event) {
    this.selectedFileGroup = event
  }
}
