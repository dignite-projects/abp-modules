import { Component, Input, OnChanges, OnDestroy } from '@angular/core';
import { IMAGE_TYPE_OPTIONS } from './models';
import { CommonModule } from '@angular/common';
import { Subscription } from 'rxjs';
import { FileDescriptorService } from '../proxy/dignite/file-explorer/files';
import { ObjectUrlService } from '../services/object-url.service';

@Component({
  // eslint-disable-next-line @angular-eslint/component-selector
  selector: 'fe-file-preview',
  templateUrl: './file-preview.component.html',
  styleUrls: ['./file-preview.component.scss'],
  imports: [CommonModule],
})
export class FilePreviewComponent implements OnChanges, OnDestroy {
  @Input() width = '100px';
  @Input() src = '';
  @Input() type = '';
  @Input() name = '';
  @Input() className = '';
  @Input() containerName = '';
  @Input() blobName = '';
  @Input() resizeWidth?: number;
  @Input() resizeHeight?: number;

  isImage = true;
  isAudio = false;
  isVideo = false;
  displaySrc = '';

  private previewObjectUrl = '';
  private streamSubscription?: Subscription;

  constructor(
    private fileDescriptorService: FileDescriptorService,
    private objectUrlService: ObjectUrlService,
  ) {}

  ngOnChanges(): void {
    this.updateFileType();
    this.updatePreviewSource();
  }

  ngOnDestroy(): void {
    this.streamSubscription?.unsubscribe();
    this.revokePreviewObjectUrl();
  }

  private updateFileType() {
    const fileName = this.name || '';
    const fileType = this.type || '';
    if (!this.type) {
      this.type = fileName.includes('.7z') ? '7z' : '';
    }
    this.isImage = fileType.includes('image/');
    this.isAudio = fileType.includes('audio/');
    this.isVideo = fileType.includes('video/');
  }

  private updatePreviewSource() {
    this.streamSubscription?.unsubscribe();
    this.revokePreviewObjectUrl();

    if (!this.isImage || this.isBrowserLocalUrl(this.src)) {
      this.displaySrc = this.src;
      return;
    }

    const fileSource = this.getFileSource();
    if (!fileSource.containerName || !fileSource.blobName) {
      this.displaySrc = this.src;
      return;
    }

    // Don't show `src` as a placeholder here: it points straight at the API (no auth header),
    // so for any container that requires a permission to Get, the browser would fetch it
    // unauthenticated and the server logs a spurious AbpAuthorizationException. Leave the
    // preview blank until the authenticated stream below resolves (or falls back on error).
    this.displaySrc = '';
    this.streamSubscription = this.fileDescriptorService
      .getStream(fileSource.containerName, fileSource.blobName, {
        width: this.resizeWidth,
        height: this.resizeHeight,
      })
      .subscribe({
        next: blob => {
          this.revokePreviewObjectUrl();
          this.previewObjectUrl = this.objectUrlService.get(blob);
          this.displaySrc = this.previewObjectUrl;
        },
        error: () => {
          this.displaySrc = this.src;
        },
      });
  }

  private getFileSource() {
    if (this.containerName && this.blobName) {
      return {
        containerName: this.containerName,
        blobName: this.blobName,
      };
    }

    return this.parseFileUrl(this.src);
  }

  private parseFileUrl(src: string) {
    if (!src) {
      return {
        containerName: '',
        blobName: '',
      };
    }

    try {
      const url = new URL(String(src), window.location.origin);
      const parts = url.pathname.split('/').filter(Boolean);
      const filesIndex = parts.findIndex(
        (part, index) =>
          part === 'files' && parts[index - 1] === 'file-explorer' && parts[index - 2] === 'api',
      );

      if (filesIndex < 0) {
        return {
          containerName: '',
          blobName: '',
        };
      }

      const containerIndex = parts[filesIndex + 1] === 'download' ? filesIndex + 2 : filesIndex + 1;
      return {
        containerName: decodeURIComponent(parts[containerIndex] ?? ''),
        blobName: parts
          .slice(containerIndex + 1)
          .map(part => decodeURIComponent(part))
          .join('/'),
      };
    } catch {
      return {
        containerName: '',
        blobName: '',
      };
    }
  }

  private isBrowserLocalUrl(src: string) {
    const value = String(src || '');
    return value.startsWith('blob:') || value.startsWith('data:');
  }

  private revokePreviewObjectUrl(): void {
    if (this.previewObjectUrl) {
      URL.revokeObjectURL(this.previewObjectUrl);
      this.previewObjectUrl = '';
    }
  }

  /** Falls back to a generic file icon when the type has no dedicated one. */
  getFileIconClass(): string {
    return IMAGE_TYPE_OPTIONS.find(item => item.type === this.type)?.icon || 'fa fa-file-o';
  }
}
