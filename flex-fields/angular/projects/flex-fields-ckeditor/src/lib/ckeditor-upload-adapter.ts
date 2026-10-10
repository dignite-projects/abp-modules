import { firstValueFrom, from } from 'rxjs';
import type { FileLoader, UploadAdapter, UploadResponse } from 'ckeditor5';
import { CKEditorUploadProvider } from './ckeditor-upload-provider';

/**
 * CKEditor 5 image-upload adapter that hands the file to the host's {@link CKEditorUploadProvider}.
 * Wired in by `CKEditorControlComponent.onReady`, only when the host registered a provider under
 * `CKEDITOR_UPLOAD_PROVIDER` and the field's `CKEditor.ImagesContainerName` is set.
 */
export class CKEditorUploadAdapter implements UploadAdapter {
  constructor(
    private readonly loader: FileLoader,
    private readonly containerName: string,
    private readonly uploadProvider: CKEditorUploadProvider,
  ) {}

  async upload(): Promise<UploadResponse> {
    const file = await this.loader.file;
    if (!file) {
      throw new Error('No file to upload.');
    }

    const url = await firstValueFrom(from(this.uploadProvider.upload(file, this.containerName)));
    if (!url) {
      throw new Error('Upload succeeded but the server did not return a file URL.');
    }

    return { default: url };
  }

  abort(): void {
    // No server-side cancellation endpoint to call. CKEditor still stops waiting on this loader's
    // upload() promise once abort() returns.
  }
}
