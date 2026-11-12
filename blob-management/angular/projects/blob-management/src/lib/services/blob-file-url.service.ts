import { EnvironmentService } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

/** The API name the BlobManagement's proxies use; without an `apis.BlobManagement` entry, ABP falls back to `apis.default`. */
export const BLOB_MANAGEMENT_API_NAME = 'BlobManagement';

/**
 * The widths and heights a Blob Management host serves by default (`BlobManagementPublicReadOptions.AllowedSizes`); any other size is
 * answered 400. A host may replace the list, but the UI cannot know it, so it asks only for these.
 */
export const BLOB_MANAGEMENT_PUBLIC_SIZES: readonly number[] = [64, 128, 256, 384, 512, 768, 1024, 1536, 2048, 4096];

/** The smallest allowed size that is at least `pixels` (the largest one when `pixels` exceeds them all). */
export function toBlobManagementPublicSize(pixels: number): number {
  return BLOB_MANAGEMENT_PUBLIC_SIZES.find(size => size >= pixels) ?? BLOB_MANAGEMENT_PUBLIC_SIZES[BLOB_MANAGEMENT_PUBLIC_SIZES.length - 1];
}

/**
 * Turns the addresses Blob Management returns into ones this UI can load. A public file's `url` is relative to the host
 * that serves the Blob Management (`/api/blob-management/public/{container}/{blob}`), which need not be this UI's own.
 */
@Injectable({
  providedIn: 'root',
})
export class BlobManagementFileUrlService {
  private readonly environment = inject(EnvironmentService);

  /** The address on the BlobManagement's host; an absolute, `blob:` or `data:` address is returned as it is. */
  toAbsolute(url: string | null | undefined): string {
    if (!url) {
      return '';
    }

    if (/^(https?:|blob:|data:)/i.test(url)) {
      return url;
    }

    const base = (this.environment.getApiUrl(BLOB_MANAGEMENT_API_NAME) ?? '').replace(/\/+$/, '');
    return base + (url.startsWith('/') ? url : '/' + url);
  }

  /**
   * A public picture asked for at a size, so the browser does not load the whole upload for a thumbnail: the Blob Management
   * renders `?Width=&Height=` (cropped to fill when both are set). Any other address is returned as it is.
   * Pass sizes from `BLOB_MANAGEMENT_PUBLIC_SIZES` (see `toBlobManagementPublicSize`): Blob Management refuses others with 400.
   */
  sized(url: string | null | undefined, width?: number, height?: number): string {
    const absolute = this.toAbsolute(url);
    if (!absolute || /^(blob:|data:)/i.test(absolute) || (!width && !height)) {
      return absolute;
    }

    const query = [width ? `Width=${width}` : '', height ? `Height=${height}` : ''].filter(Boolean).join('&');
    return absolute + (absolute.includes('?') ? '&' : '?') + query;
  }
}
