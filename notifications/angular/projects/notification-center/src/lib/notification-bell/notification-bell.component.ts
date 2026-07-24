import { ChangeDetectorRef, Component, NgZone, OnDestroy, OnInit, Type, inject } from '@angular/core';
import { DOCUMENT, DatePipe, NgComponentOutlet } from '@angular/common';
import { AuthService, EnvironmentService, LocalizationPipe } from '@abp/ng.core';
import { NgbDropdownModule } from '@ng-bootstrap/ng-bootstrap';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { Router } from '@angular/router';
import { UserNotificationService, UserNotificationDto } from '../proxy/dignite/abp/notification-center';
import { NotificationSeverity, UserNotificationState } from '../proxy/dignite/abp/notifications';
import { NotificationDataComponentsService } from '../notification-data/notification-data-components.service';
import { NotificationDataPayload } from '../notification-data/notification-data-payload';
import {
  NotificationEntityLinkTarget,
  NotificationEntityLinksService,
} from '../notification-links/notification-entity-links.service';

/**
 * Notification bell: unread badge + dropdown of recent unread notifications, with mark-as-read / mark-all-as-read.
 * Refreshes on startup and when the ABP-mapped SignalR hub receives a notification (auto-reconnect handled by the
 * SignalR client). Each item's body is dispatched by discriminator through NotificationDataComponentsService
 * (mirrors the MVC UI's NotificationCenterWebOptions.DataViewComponents), falling back to a generic image-only
 * rendering when no renderer is registered for it. Server-side tolerant placeholders use the built-in
 * "Dignite.Unsupported" renderer, so unreadable payloads still produce a visible safe fallback.
 */
@Component({
  selector: 'abp-notification-bell',
  standalone: true,
  imports: [DatePipe, LocalizationPipe, NgComponentOutlet, NgbDropdownModule],
  template: `
    <div
      class="dropdown abp-notification-bell"
      ngbDropdown
      #notificationDropdown="ngbDropdown"
      display="static"
      autoClose="outside"
      (openChange)="onDropdownOpenChange($event)"
    >
      <a
        class="nav-link abp-notification-bell-btn"
        href="javascript:void(0)"
        role="button"
        ngbDropdownToggle
        [class.dropdown-toggle]="false"
        aria-haspopup="true"
        [attr.aria-label]="'NotificationCenter::Notifications' | abpLocalization"
      >
        <i aria-hidden="true" class="lpx-icon fas fa-bell"></i>
        @if (unreadCount > 0) {
          <span class="abp-notification-badge">{{ unreadCount }}</span>
        }
      </a>
      <div
        ngbDropdownMenu
        class="dropdown-menu-end border-0 shadow-sm abp-notification-dropdown"
        [class.d-block]="smallScreen && notificationDropdown.isOpen()"
      >
        <div class="abp-notification-dropdown-header">
          <strong>{{ 'NotificationCenter::Notifications' | abpLocalization }}</strong>
          @if (unreadCount > 0) {
            <a href="#" (click)="markAllAsRead(); $event.preventDefault()">{{
              'NotificationCenter::MarkAllAsRead' | abpLocalization
            }}</a>
          }
        </div>
        @if (notifications.length === 0) {
          <div class="abp-notification-empty">{{ 'NotificationCenter::NoUnreadNotifications' | abpLocalization }}</div>
        } @else {
          @for (n of notifications; track n.id) {
            <div
              class="abp-notification-item"
              [class.abp-notification-unread]="n.state === 0"
              role="button"
              tabindex="0"
              (click)="onNotificationClick(n)"
              (keydown.enter)="onNotificationClick(n)"
              (keydown.space)="onNotificationClick(n); $event.preventDefault()"
            >
              <div class="abp-notification-item-header">
                <span [class]="notificationTitleClassOf(n)">{{ n.notificationDisplayName || n.notificationName }}</span>
                <span class="abp-notification-item-time">{{ n.creationTime | date: 'short' }}</span>
              </div>
              @if (dataComponentOf(n); as dataComponent) {
                <ng-container [ngComponentOutlet]="dataComponent" [ngComponentOutletInputs]="{ data: n.data }" />
              } @else if (imageUrlOf(n); as img) {
                <img class="abp-notification-item-image" [src]="img" alt="" />
              }
            </div>
          }
        }
      </div>
    </div>
  `,
  styles: [
    `
      .abp-notification-bell { position: relative; display: inline-block; }
      .abp-notification-bell-btn { cursor: pointer; position: relative; font-size: 1.2rem; }
      .abp-notification-badge { position: absolute; top: -4px; right: -8px; background: #dc3545; color: #fff; border-radius: 10px; padding: 0 5px; font-size: 0.7rem; }
      .abp-notification-dropdown { position: absolute; right: 0; top: 100%; min-width: 380px; max-height: 400px; overflow-y: auto; background: #fff; border: 1px solid #ddd; border-radius: 4px; box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15); z-index: 1000; }
      .abp-notification-dropdown-header { display: flex; justify-content: space-between; align-items: center; padding: 8px; border-bottom: 1px solid #eee; }
      .abp-notification-empty { padding: 12px; color: #888; }
      .abp-notification-item { padding: 8px; border-bottom: 1px solid #f0f0f0; cursor: pointer; }
      .abp-notification-item.abp-notification-unread { background: rgba(13, 110, 253, 0.05); }
      .abp-notification-item-header { display: grid; grid-template-columns: minmax(0, 1fr) max-content; align-items: baseline; gap: 8px; }
      .abp-notification-item-title { min-width: 0; overflow-wrap: anywhere; font-size: 0.9rem; }
      .abp-notification-item-time { font-size: 0.75rem; color: #888; white-space: nowrap; }
      .abp-notification-item-image { max-width: 100%; border-radius: 4px; margin-top: 4px; }
    `,
  ],
})
export class NotificationBellComponent implements OnInit, OnDestroy {
  unreadCount = 0;
  notifications: UserNotificationDto[] = [];

  private markingNotificationIds = new Set<string>();
  private notificationService = inject(UserNotificationService);
  private notificationDataComponents = inject(NotificationDataComponentsService);
  private notificationEntityLinks = inject(NotificationEntityLinksService);
  private authService = inject(AuthService);
  private environmentService = inject(EnvironmentService);
  private ngZone = inject(NgZone);
  private document = inject(DOCUMENT);
  private changeDetectorRef = inject(ChangeDetectorRef);
  private router = inject(Router);
  private hubConnection?: HubConnection;
  private startPromise?: Promise<void>;

  get smallScreen(): boolean {
    return (this.document.defaultView?.innerWidth ?? 0) < 992;
  }

  ngOnInit(): void {
    this.refresh();
    this.startRealtime();
  }

  ngOnDestroy(): void {
    void this.hubConnection?.stop();
  }

  /** Public: a host app can force a refresh after local actions. SignalR updates are handled by this component. */
  refresh(): void {
    this.notificationService.getUnreadCount().subscribe(c => {
      this.unreadCount = c;
      this.changeDetectorRef.markForCheck();
    });
    this.notificationService.getList({ state: UserNotificationState.Unread, maxResultCount: 10 }).subscribe(r => {
      this.notifications = r.items;
      this.changeDetectorRef.markForCheck();
    });
  }

  onNotificationClick(n: UserNotificationDto): void {
    const target = this.notificationEntityLinks.resolve(n);
    this.markAsRead(n, target);
  }

  onDropdownOpenChange(isOpen: boolean): void {
    if (isOpen) {
      this.refresh();
    }
  }

  markAsRead(n: UserNotificationDto, target: NotificationEntityLinkTarget | null = null): void {
    if (!n.notificationId || n.state === UserNotificationState.Read) {
      this.navigateToTarget(target);
      return;
    }

    if (this.markingNotificationIds.has(n.notificationId)) {
      return;
    }
    const notificationId = n.notificationId;

    this.markingNotificationIds.add(notificationId);
    this.notificationService.markAsRead(notificationId).subscribe(() => {
      this.markingNotificationIds.delete(notificationId);
      n.state = UserNotificationState.Read;
      this.unreadCount = Math.max(0, this.unreadCount - 1);
      this.changeDetectorRef.markForCheck();
      if (target) {
        this.navigateToTarget(target);
      }
    }, () => {
      this.markingNotificationIds.delete(notificationId);
      this.changeDetectorRef.markForCheck();
      this.navigateToTarget(target);
    });
  }

  markAllAsRead(): void {
    this.notificationService.markAllAsRead().subscribe(() => {
      this.notifications = this.notifications.map(notification => ({
        ...notification,
        state: UserNotificationState.Read,
      }));
      this.unreadCount = 0;
      this.changeDetectorRef.markForCheck();
    });
  }

  /** Duck-typed image URL (mirrors IHasNotificationImageUrl's "imageUrl" JSON property). */
  imageUrlOf(n: UserNotificationDto): string | null {
    const url = (n.data as NotificationDataPayload | null | undefined)?.['imageUrl'];
    return typeof url === 'string' ? url : null;
  }

  /** The renderer registered for this item's discriminator, or null to fall back to imageUrlOf(). */
  dataComponentOf(n: UserNotificationDto): Type<unknown> | null {
    return this.notificationDataComponents.get(
      (n.data as NotificationDataPayload | null | undefined)?.type,
    );
  }

  notificationTitleClassOf(n: UserNotificationDto): string {
    const severityClass = this.severityTextClassOf(n.severity);
    return `abp-notification-item-title ${severityClass}`;
  }

  private severityTextClassOf(severity: NotificationSeverity | undefined): string {
    switch (severity) {
      case NotificationSeverity.Success:
        return 'text-success';
      case NotificationSeverity.Warn:
        return 'text-warning';
      case NotificationSeverity.Error:
        return 'text-danger';
      default:
        return 'text-info';
    }
  }

  private navigateToTarget(target: NotificationEntityLinkTarget | null): void {
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

  private startRealtime(): void {
    if (!this.authService.isAuthenticated || this.hubConnection || this.startPromise) {
      return;
    }

    const apiUrl = this.environmentService.getApiUrl(this.notificationService.apiName).replace(/\/$/, '');
    this.hubConnection = new HubConnectionBuilder()
      .withUrl(`${apiUrl}/signalr-hubs/notifications`, {
        accessTokenFactory: () => this.authService.getAccessToken(),
      })
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('ReceiveNotification', () => {
      this.ngZone.run(() => this.refresh());
    });
    this.hubConnection.onreconnected(() => {
      this.ngZone.run(() => this.refresh());
    });

    this.startPromise = this.hubConnection.start().catch(err => {
      this.hubConnection = undefined;
      console.warn('Notification hub connection failed.', err);
    }).finally(() => {
      this.startPromise = undefined;
    });
  }
}
