import { CoreModule } from '@abp/ng.core';
import { ChangeDetectionStrategy, Component, input, model, signal } from '@angular/core';
import { FormatFileSizePipe } from '../../pipes/format-file-size.pipe';
import { BlobManagementFileDto } from '../../proxy/files';
import { BlobManagementFilePreviewComponent } from '../blob-file-preview/blob-file-preview.component';
import { BlobManagementFilePickerModalComponent } from './blob-file-picker-modal.component';

/**
 * Chooses files from a container for a form: a button that opens the picker, and the files chosen, each removable.
 * Bind the choice with `[(value)]`; a file of a public container carries its `url`, any file its `id`.
 */
@Component({
  selector: 'blob-file-picker',
  changeDetection: ChangeDetectionStrategy.Eager,
  template: `
    <button type="button" class="btn btn-sm btn-outline-primary" (click)="modalOpen.set(true)">
      <i class="fa fa-folder-open me-1" aria-hidden="true"></i>{{ 'BlobManagement::SelectFiles' | abpLocalization }}
    </button>

    @if (value().length > 0) {
      <ul class="list-group mt-2">
        @for (file of value(); track file.id) {
          <li class="list-group-item d-flex align-items-center gap-2">
            <blob-file-preview [src]="file.url" [mimeType]="file.mimeType" [name]="file.name" [pixels]="32" />
            <span class="flex-grow-1 text-break">{{ file.name }}</span>
            <small class="text-muted">{{ file.size | blobFileSize }}</small>
            <button
              type="button"
              class="btn btn-sm btn-outline-secondary"
              [attr.aria-label]="'BlobManagement::RemoveFile' | abpLocalization: file.name ?? ''"
              (click)="remove(file)"
            >
              <i class="fa fa-times" aria-hidden="true"></i>
            </button>
          </li>
        }
      </ul>
    } @else {
      <div class="text-muted small mt-2">{{ 'BlobManagement::NoFileSelected' | abpLocalization }}</div>
    }

    <blob-file-picker-modal
      [(visible)]="modalOpen"
      [containerName]="containerName()"
      [multiple]="multiple()"
      [value]="value()"
      (confirmed)="value.set($event)"
    />
  `,
  imports: [CoreModule, FormatFileSizePipe, BlobManagementFilePreviewComponent, BlobManagementFilePickerModalComponent],
})
export class BlobManagementFilePickerComponent {
  /** Only this container; every container the caller has something to do in when empty. */
  readonly containerName = input<string | null | undefined>(null);
  readonly multiple = input(false);
  /** The files chosen. */
  readonly value = model<BlobManagementFileDto[]>([]);

  readonly modalOpen = signal(false);

  remove(file: BlobManagementFileDto): void {
    this.value.set(this.value().filter(item => item.id !== file.id));
  }
}
