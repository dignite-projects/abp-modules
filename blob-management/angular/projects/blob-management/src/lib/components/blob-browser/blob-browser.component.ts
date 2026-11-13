import { CoreModule } from '@abp/ng.core';
import { ChangeDetectionStrategy, Component, effect, input, signal, untracked } from '@angular/core';
import { BlobManagementContainerDto } from '../../proxy/containers';
import { BlobManagementDirectoryDto } from '../../proxy/directories';
import { BlobManagementDirectoryTreeComponent } from '../blob-directory-tree/blob-directory-tree.component';
import { BlobManagementFileListComponent } from '../blob-file-list/blob-file-list.component';

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
          <blob-file-list [container]="container()" [directory]="directory()" />
        </div>
      </div>
    } @else {
      <blob-file-list [container]="container()" />
    }
  `,
  imports: [CoreModule, BlobManagementDirectoryTreeComponent, BlobManagementFileListComponent],
})
export class BlobManagementBrowserComponent {
  readonly container = input.required<BlobManagementContainerDto>();

  /** The directory shown; `null` for the top of the container. */
  readonly directory = signal<BlobManagementDirectoryDto | null>(null);

  constructor() {
    effect(() => {
      this.container();
      untracked(() => this.directory.set(null));
    });
  }
}
