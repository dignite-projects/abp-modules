import { CoreModule } from '@abp/ng.core';
import { ChangeDetectionStrategy, Component, computed, effect, input, signal, untracked } from '@angular/core';
import { BlobManagementContainerDto, BlobManagementDirectoryDto } from '@dignite/ng.blob-management/proxy';
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

  /**
   * Which container is shown. The container's object changes when what the caller may do with it does; the directory
   * chosen is forgotten only when another container is shown.
   */
  readonly containerName = computed(() => this.container().name ?? '');

  /** The directory shown; `null` for the top of the container. */
  readonly directory = signal<BlobManagementDirectoryDto | null>(null);

  constructor() {
    effect(() => {
      this.containerName();
      untracked(() => this.directory.set(null));
    });
  }
}
