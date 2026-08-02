import { Component } from '@angular/core';
import {
  FileDescriptorDto,
  FileExplorerUploadComponent,
  FileExplorerPickerComponent,
  FileUploadChangeEvent,
} from '@dignite/ng.file-explorer';

@Component({
  // eslint-disable-next-line @angular-eslint/component-selector
  selector: 'app-file-upload-demo',
  templateUrl: './file-upload-demo.component.html',
  styleUrls: ['./file-upload-demo.component.scss'],
  imports: [FileExplorerUploadComponent, FileExplorerPickerComponent],
})
export class FileUploadDemoComponent {
  /** Mirrors what a host form would submit: already-uploaded files plus pending additions/deletions. */
  fileDataToBeSubmitted: FileUploadChangeEvent | undefined;

  onFileDataChange(event: FileUploadChangeEvent): void {
    this.fileDataToBeSubmitted = event;
  }

  selectedFileGroup: FileDescriptorDto[] = [];

  onSelectedFileChange(files: FileDescriptorDto[]): void {
    this.selectedFileGroup = files;
  }
}
