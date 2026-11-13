import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { BlobManagementFileUrlService, toBlobManagementPublicSize } from '../../services/blob-file-url.service';

const ICONS: Record<string, string> = {
  'application/pdf': 'fa-file-pdf',
  'application/msword': 'fa-file-word',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document': 'fa-file-word',
  'application/vnd.ms-excel': 'fa-file-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': 'fa-file-excel',
  'application/vnd.ms-powerpoint': 'fa-file-powerpoint',
  'application/vnd.openxmlformats-officedocument.presentationml.presentation': 'fa-file-powerpoint',
  'application/zip': 'fa-file-archive',
  'text/plain': 'fa-file-alt',
  'text/csv': 'fa-file-csv',
};

/**
 * A thumbnail or a type icon for one file. A picture is shown when there is an address to show it from - a public
 * file's, asked for at the thumbnail's size, or a local `blob:` address of a file not uploaded yet; a file of a
 * container that is not public has none, and gets its type's icon.
 */
@Component({
  selector: 'blob-file-preview',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="blob-file-preview" [style.width]="size()" [style.height]="size()">
      @if (imageSrc()) {
        <img [src]="imageSrc()" [alt]="name()" />
      } @else {
        <i class="fa {{ icon() }}" aria-hidden="true"></i>
      }
    </div>
  `,
  styles: [
    `
      .blob-file-preview {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        overflow: hidden;
        font-size: 1.5rem;
        color: var(--bs-secondary-color, #6c757d);
      }
      .blob-file-preview img {
        width: 100%;
        height: 100%;
        object-fit: cover;
        border-radius: 0.25rem;
      }
    `,
  ],
})
export class BlobManagementFilePreviewComponent {
  private readonly urls = inject(BlobManagementFileUrlService);

  /** The file's address: a public address, or a local `blob:` one; empty for a file that is not public. */
  readonly src = input<string | null | undefined>('');
  readonly mimeType = input<string | null | undefined>('');
  readonly name = input<string | null | undefined>('');
  /** The thumbnail's side, in pixels. */
  readonly pixels = input(48);

  readonly size = computed(() => `${this.pixels()}px`);

  readonly isImage = computed(() => (this.mimeType() ?? '').startsWith('image/'));

  readonly imageSrc = computed(() => {
    const src = this.src();
    if (!src || !this.isImage()) {
      return '';
    }

    // Twice the side, for high-density screens, rounded up to a size the server serves; the CSS size stays `pixels`.
    const side = toBlobManagementPublicSize(this.pixels() * 2);
    return this.urls.sized(src, side, side);
  });

  readonly icon = computed(() => {
    const type = this.mimeType() ?? '';
    if (type.startsWith('image/')) return 'fa-file-image';
    if (type.startsWith('audio/')) return 'fa-file-audio';
    if (type.startsWith('video/')) return 'fa-file-video';
    return ICONS[type] ?? 'fa-file';
  });
}
