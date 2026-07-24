import { Injectable } from '@angular/core';

/**
 * Creates a short-lived object URL for a Blob so previews can reference the
 * browser-managed blob instead of holding a base64 copy in memory. Callers own
 * the returned URL and must revoke it (URL.revokeObjectURL) when it is no longer
 * needed to avoid leaking the underlying blob.
 */
@Injectable({
  providedIn: 'root',
})
export class ObjectUrlService {
  get(file: Blob): string {
    return URL.createObjectURL(file);
  }
}
