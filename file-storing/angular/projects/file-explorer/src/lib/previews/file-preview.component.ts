import { Component, Input, OnChanges, OnDestroy, TemplateRef } from '@angular/core';
import { ImageTypeOption } from './models';
import { NgbModal, NgbModalRef } from '@ng-bootstrap/ng-bootstrap';
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

  /**文件宽度 */
  @Input() width: any = '100px'
  /*文件链接 */
  @Input() src: any = ''
  /**是否支持大图预览 */
  @Input() preview: any = true;
  /**文件类型 */
  @Input() type: any = ''
  /**文件名称 */
  @Input() name: any = ''
  @Input() className: any = ''
  @Input() containerName: any = ''
  @Input() blobName: any = ''
  @Input() resizeWidth: any
  @Input() resizeHeight: any

  /**是否是文件 */
  isImage = true
  /**是否是视频 */
  isAudio = false
  /**是否是音频 */
  isVideo = false
  /**文件类型及图标 */
  _ImageTypeOption = ImageTypeOption
  displaySrc = ''
  private previewObjectUrl = ''
  private streamSubscription?: Subscription


  constructor(
    private modalService: NgbModal,
    private fileDescriptorService: FileDescriptorService,
    private objectUrlService: ObjectUrlService,
  ) { }
  
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
      this.type = fileName.includes('.7z') ? '7z' : ''
    }
    this.isImage = fileType.includes('image/')
    this.isAudio = fileType.includes('audio/')
    this.isVideo = fileType.includes('video/')
  }

  private updatePreviewSource() {
    this.streamSubscription?.unsubscribe();
    this.revokePreviewObjectUrl();
    this.displaySrc = this.src;

    if (!this.isImage || this.isBrowserLocalUrl(this.src)) {
      return;
    }

    const fileSource = this.getFileSource();
    if (!fileSource.containerName || !fileSource.blobName) {
      return;
    }

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
      const filesIndex = parts.findIndex((part, index) =>
        part === 'files' && parts[index - 1] === 'file-explorer' && parts[index - 2] === 'api'
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
        blobName: parts.slice(containerIndex + 1).map(part => decodeURIComponent(part)).join('/'),
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

  private revokePreviewObjectUrl() {
    if (this.previewObjectUrl) {
      URL.revokeObjectURL(this.previewObjectUrl);
      this.previewObjectUrl = '';
    }
  }

  /**预览图片 */
  previewImage() {

  }

  /**模态框实例 */
  modalRef!: NgbModalRef;

  /**放大倍数 */
  zoom: number = 10
  /**旋转 */
  rotate: number = 0


  /**打开预览弹窗 */
  OpenPreviewImage(content: TemplateRef<any>) {

    this.modalRef = this.modalService.open(content, {
      fullscreen: true,
      modalDialogClass: 'dignite-preview',
    });
    this.modalRef.result.then(
      (result) => {
      },
      (reason) => {
        this.zoom = 10
      },
    );
  }
  /**放大图像 */
  zoomIn() {
    let zoom = this.zoom
    if (zoom == 20) return
    zoom++
    this.zoom = zoom
  }
  /**缩小图像 */
  zoomOut() {
    let zoom = this.zoom
    if (zoom == 3) return
    zoom--
    this.zoom = zoom
  }
  /**右旋转 */
  RotateRight() {
    if (this.rotate == 360) return this.rotate = 0
    this.rotate += 90
  }
}
