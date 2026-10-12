import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { NotificationEntityLinkTarget } from './notification-entity-links.service';

/**
 * Follows a resolved {@link NotificationEntityLinkTarget}: route arrays and UrlTrees go through the router, a
 * same-origin absolute URL is routed by its path, and a cross-origin or protocol-relative URL leaves the SPA.
 * Shared by the bell and the inbox page so both treat entity links identically.
 */
@Injectable({ providedIn: 'root' })
export class NotificationNavigationService {
  private router = inject(Router);
  private document = inject(DOCUMENT);

  navigate(target: NotificationEntityLinkTarget | null): void {
    if (!target) {
      return;
    }

    if (typeof target === 'string') {
      this.navigateToStringTarget(target);
      return;
    }

    if (Array.isArray(target)) {
      void this.router.navigate(target);
      return;
    }

    void this.router.navigateByUrl(target);
  }

  private navigateToStringTarget(target: string): void {
    const trimmedTarget = target.trim();
    if (!trimmedTarget) {
      return;
    }

    const windowRef = this.document.defaultView;
    if (this.isExternalUrl(trimmedTarget, windowRef)) {
      windowRef?.location.assign(trimmedTarget);
      return;
    }

    if (/^[a-z][a-z0-9+.-]*:/i.test(trimmedTarget) && windowRef) {
      const url = new URL(trimmedTarget);
      void this.router.navigateByUrl(`${url.pathname}${url.search}${url.hash}`);
      return;
    }

    void this.router.navigateByUrl(trimmedTarget);
  }

  private isExternalUrl(target: string, windowRef: Window | null): boolean {
    if (target.startsWith('//')) {
      return true;
    }

    if (!/^[a-z][a-z0-9+.-]*:/i.test(target)) {
      return false;
    }

    if (!windowRef) {
      return true;
    }

    try {
      return new URL(target).origin !== windowRef.location.origin;
    } catch {
      return true;
    }
  }
}
