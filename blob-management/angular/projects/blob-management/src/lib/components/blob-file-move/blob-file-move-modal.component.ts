import { CoreModule } from '@abp/ng.core';
import { ThemeSharedModule } from '@abp/ng.theme.shared';
import { ChangeDetectionStrategy, Component, computed, inject, input, model, output, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { BlobManagementContainerDto } from '../../proxy/containers';
import { BlobManagementDirectoryDto } from '../../proxy/directories';
import { BlobManagementFileDto, BlobManagementFileService } from '../../proxy/files';
import { BlobManagementDirectoryTreeComponent } from '../blob-directory-tree/blob-directory-tree.component';

/**
 * Moves one file to another directory of its container, or to the top of it: the container's directories as a tree to
 * choose from, and the move. A directory is offered only where the user may upload - the server asks that, and the
 * right to move the file, again. Open while `file` is set.
 */
@Component({
  selector: 'blob-file-move-modal',
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <abp-modal [visible]="!!file()" (visibleChange)="$event || close()" [busy]="busy()">
      <ng-template #abpHeader>
        <h3>{{ 'BlobManagement::MoveFile' | abpLocalization: file()?.name ?? '' }}</h3>
      </ng-template>
      <ng-template #abpBody>
        <p class="text-muted">{{ 'BlobManagement::MoveFileDestination' | abpLocalization }}</p>
        <blob-directory-tree [container]="container()" [manage]="false" (selectionChange)="target.set($event)" />
      </ng-template>
      <ng-template #abpFooter>
        <button type="button" class="btn btn-secondary" abpClose>{{ 'AbpUi::Cancel' | abpLocalization }}</button>
        <button type="button" class="btn btn-primary" [disabled]="!canMove() || busy()" (click)="move()">
          <i class="fa fa-check me-1" aria-hidden="true"></i>{{ 'BlobManagement::Move' | abpLocalization }}
        </button>
      </ng-template>
    </abp-modal>
  `,
  imports: [CoreModule, ThemeSharedModule, BlobManagementDirectoryTreeComponent],
})
export class BlobManagementFileMoveModalComponent {
  private readonly fileService = inject(BlobManagementFileService);

  readonly container = input.required<BlobManagementContainerDto>();
  /** The file to move; the modal is open while it is set, and clears it when it closes. */
  readonly file = model<BlobManagementFileDto | null>(null);
  /** The file as the server returns it, once moved. */
  readonly moved = output<BlobManagementFileDto>();

  /** Where the file goes: a directory, or `null` for the top of the container. */
  readonly target = signal<BlobManagementDirectoryDto | null>(null);
  readonly busy = signal(false);

  /** Somewhere else than where the file is, and where the user may upload. */
  readonly canMove = computed(() => {
    const file = this.file();
    if (!file?.id) {
      return false;
    }

    const target = this.target();
    if ((target?.id ?? null) === (file.directoryId ?? null)) {
      return false;
    }

    return !!(target ? target.access?.uploadFiles : this.container().access?.uploadFiles);
  });

  move(): void {
    const id = this.file()?.id;
    if (!id || !this.canMove() || this.busy()) {
      return;
    }

    this.busy.set(true);
    this.fileService
      .move(id, { directoryId: this.target()?.id ?? null })
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe(file => {
        this.moved.emit(file);
        this.close();
      });
  }

  close(): void {
    this.target.set(null);
    this.file.set(null);
  }
}
