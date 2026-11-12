import { CoreModule } from '@abp/ng.core';
import { ChangeDetectionStrategy, Component, effect, input, model, signal, untracked } from '@angular/core';
import { BlobManagementContainerDto } from '../../proxy/containers';
import { BlobManagementDirectoryDto } from '../../proxy/directories';
import { BlobManagementFileDto } from '../../proxy/files';
import { BlobManagementDirectoryTreeComponent } from '../blob-directory-tree/blob-directory-tree.component';
import { BlobManagementFileListComponent, BlobManagementFileSelection } from '../blob-file-list/blob-file-list.component';

/**
 * One container: its files, and - when it allows directories - its directory tree beside the files of the directory
 * chosen. A container without directories is one flat list.
 */
@Component({
  selector: 'blob-browser',
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    @if (container().allowDirectories) {
      <div class="row g-3">
        <div class="col-md-4 col-lg-3">
          <blob-directory-tree [container]="container()" (selectionChange)="directory.set($event)" />
        </div>
        <div class="col-md-8 col-lg-9">
          <blob-file-list
            [container]="container()"
            [directory]="directory()"
            [selection]="selection()"
            [(selected)]="selected"
          />
        </div>
      </div>
    } @else {
      <blob-file-list [container]="container()" [selection]="selection()" [(selected)]="selected" />
    }
  `,
  imports: [CoreModule, BlobManagementDirectoryTreeComponent, BlobManagementFileListComponent],
})
export class BlobManagementBrowserComponent {
  readonly container = input.required<BlobManagementContainerDto>();
  readonly selection = input<BlobManagementFileSelection>('none');
  readonly selected = model<BlobManagementFileDto[]>([]);

  /** The directory shown; `null` for the top of the container. */
  readonly directory = signal<BlobManagementDirectoryDto | null>(null);

  constructor() {
    effect(() => {
      this.container();
      untracked(() => this.directory.set(null));
    });
  }
}
