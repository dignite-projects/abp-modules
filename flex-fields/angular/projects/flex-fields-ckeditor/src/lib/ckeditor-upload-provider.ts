import { InjectionToken } from '@angular/core';
import type { Observable } from 'rxjs';

/**
 * Uploads an image inserted into a CKEditor field and says what URL the editor should embed for it.
 *
 * The flex-fields packages ship no upload API and no implementation of this - the host application
 * owns both, and registers its implementation under {@link CKEDITOR_UPLOAD_PROVIDER}:
 *
 * ```ts
 * @Injectable({ providedIn: 'root' })
 * export class MyCKEditorUploadProvider implements CKEditorUploadProvider {
 *   private readonly restService = inject(RestService);
 *
 *   upload(file: File, containerName: string): Observable<string> {
 *     const body = new FormData();
 *     body.append('file', file, file.name);
 *     return this.restService
 *       .request<FormData, { url: string }>(
 *         { method: 'POST', url: '/api/my-app/files', params: { containerName }, body },
 *         { apiName: 'MyApp' },
 *       )
 *       .pipe(map(result => result.url));
 *   }
 * }
 *
 * providers: [
 *   provideFlexFields(),
 *   provideCKEditorFieldType(),
 *   { provide: CKEDITOR_UPLOAD_PROVIDER, useExisting: MyCKEditorUploadProvider },
 * ]
 * ```
 *
 * With no provider registered, the image-upload toolbar button is not shown - the same as for a field
 * whose `CKEditor.ImagesContainerName` is empty.
 */
export interface CKEditorUploadProvider {
  /**
   * Uploads `file` into the blob container the field names in `CKEditor.ImagesContainerName` and
   * resolves to the URL the editor embeds as `<img src>` (or `![](url)` in Markdown).
   */
  upload(file: File, containerName: string): Observable<string> | Promise<string>;
}

export const CKEDITOR_UPLOAD_PROVIDER = new InjectionToken<CKEditorUploadProvider>('CKEDITOR_UPLOAD_PROVIDER');
