import { Component, Input } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { FlexFieldValue } from '@dignite/ng.flex-fields';
import { FilePreviewComponent } from '@dignite/ng.file-explorer';

interface FileExplorerFileValue {
  url?: string;
  containerName?: string;
  blobName?: string;
  name?: string;
  mimeType?: string;
}

/** Displays the value of a `FileExplorer` field read-only: the first file's preview, plus a count. */
@Component({
  selector: 'ff-file-explorer-view',
  templateUrl: './file-explorer-view.component.html',
  imports: [CoreModule, FilePreviewComponent],
})
export class FileExplorerViewComponent {
  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type, e.g. `FileExplorer`. */
  @Input() type?: string;

  @Input() value: unknown = '';

  get files(): FileExplorerFileValue[] {
    return Array.isArray(this.value) ? this.value : [];
  }
}
