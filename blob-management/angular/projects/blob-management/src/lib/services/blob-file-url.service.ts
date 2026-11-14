import { EnvironmentService } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

/** The API name Blob Management's proxies use; without an `apis.BlobManagement` entry, ABP falls back to `apis.default`. */
export const BLOB_MANAGEMENT_API_NAME = 'BlobManagement';

/**
 * The smallest of `sizes` that is at least `pixels` (the largest one when `pixels` exceeds them all), or `undefined`
 * when there are none. `sizes` are the container's public sizes (`BlobManagementContainerDto.publicSizes`): Blob
 * Management answers any other width or height with 400.
 */
export function toBlobManagementPublicSize(pixels: number, sizes: readonly number[] | null | undefined): number | undefined {
  const sorted = [...(sizes ?? [])].sort((a, b) => a - b);
  return sorted.find(size => size >= pixels) ?? sorted[sorted.length - 1];
}

/**
 * Turns the addresses Blob Management returns into ones this UI can load. A public file's `url` is relative to the host
 * that serves the Blob Management (`/blobs/{container}/{blob}`), which need not be this UI's own.
 */
@Injectable({
  providedIn: 'root',
})
export class BlobManagementFileUrlService {
  private readonly environment = inject(EnvironmentService);

  /** The address on Blob Management's host; an absolute, `blob:` or `data:` address is returned as it is. */
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
   * Pass sizes of the file's container (see `toBlobManagementPublicSize`): Blob Management refuses others with 400.
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
