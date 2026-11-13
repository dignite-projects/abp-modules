import { Injectable } from '@angular/core';

/**
 * Creates an object URL for a Blob, so a preview or a download refers to the browser-managed blob instead of a base64
 * copy in memory. The caller owns the URL and revokes it (`URL.revokeObjectURL`) when it is no longer shown.
 */
@Injectable({
  providedIn: 'root',
})
export class ObjectUrlService {
  get(file: Blob): string {
    return URL.createObjectURL(file);
  }

  revoke(url: string | null | undefined): void {
    if (url?.startsWith('blob:')) {
      URL.revokeObjectURL(url);
    }
  }
}
