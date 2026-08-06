import { firstValueFrom } from 'rxjs';
import { RestService } from '@abp/ng.core';
import type { FileLoader, UploadAdapter, UploadResponse } from 'ckeditor5';

/**
 * Minimal shape of Dignite.FileExplorer's `FileDescriptorDto` this adapter actually reads. Declared
 * locally rather than imported from `@dignite/ng.file-explorer`: that package's proxy service is not
 * part of its public API (see its `public-api.ts`), and pulling in the whole package for one REST call
 * would be exactly the dependency weight `@dignite/ng.flex-fields-file-explorer`'s own README says to
 * avoid paying for unless you need the picker UI.
 */
interface UploadedFileDescriptor {
  url?: string;
}

/**
 * CKEditor 5 image-upload adapter posting to Dignite.FileExplorer's existing file upload API. The wire
 * contract here is copied from the actual, working call in `@dignite/ng.file-explorer`'s own
 * `FileExplorerModalComponent.uploadFile()` (a `FormData` with a single `file` field, `containerName`
 * as a query param, `POST /api/file-explorer/files`, response is a `FileDescriptorDto` with `url`) -
 * not inferred from the legacy dignite-abp CKEditor integration. Wired in by
 * `CKEditorControlComponent.onReady`, only when `CKEditor.ImagesContainerName` is set.
 */
export class CKEditorUploadAdapter implements UploadAdapter {
  constructor(
    private readonly loader: FileLoader,
    private readonly containerName: string,
    private readonly restService: RestService,
  ) {}

  async upload(): Promise<UploadResponse> {
    const file = await this.loader.file;
    if (!file) {
      throw new Error('No file to upload.');
    }

    const body = new FormData();
    body.append('file', file, file.name);

    const descriptor = await firstValueFrom(
      this.restService.request<FormData, UploadedFileDescriptor>(
        {
          method: 'POST',
          url: '/api/file-explorer/files',
          params: { containerName: this.containerName },
          body,
        },
        { apiName: 'FileExplorer' },
      ),
    );

    if (!descriptor.url) {
      throw new Error('Upload succeeded but the server did not return a file URL.');
    }

    return { default: descriptor.url };
  }

  abort(): void {
    // No server-side cancellation endpoint to call. CKEditor still stops waiting on this loader's
    // upload() promise once abort() returns.
  }
}
