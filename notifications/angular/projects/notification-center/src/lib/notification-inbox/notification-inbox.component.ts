import { ChangeDetectorRef, Component, OnInit, Type, inject } from '@angular/core';
import { DatePipe, NgComponentOutlet } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { NgbPagination } from '@ng-bootstrap/ng-bootstrap';
import { Observable } from 'rxjs';
import {
  UserNotificationDto,
  UserNotificationGroupDto,
  UserNotificationService,
} from '../proxy/dignite/abp/notification-center';
import { NotificationSeverity, UserNotificationState } from '../proxy/dignite/abp/notifications';
import { NotificationDataComponentsService } from '../notification-data/notification-data-components.service';
import { NotificationDataPayload } from '../notification-data/notification-data-payload';
import { NotificationEntityLinksService } from '../notification-links/notification-entity-links.service';
import { NotificationNavigationService } from '../notification-links/notification-navigation.service';
import { NotificationCenterEventsService } from './notification-center-events.service';

/**
 * The current user's notification inbox: group tabs with unread counts, an all/unread filter and a paged list.
 * Clicking an item marks it read and follows its entity link — the item stays listed. Deletes and the bulk
 * actions (mark all read, clear read) ask for confirmation and reload the list. Routed through `createRoutes()`
 * and reached from the bell, not from the main menu.
 */
@Component({
  selector: 'abp-notification-inbox',
  standalone: true,
  imports: [DatePipe, LocalizationPipe, NgComponentOutlet, NgbPagination],
  template: `
    <div class="card abp-notification-inbox">
      <div class="card-header">
        <ul class="nav nav-tabs card-header-tabs">
          <li class="nav-item">
            <a
              href="javascript:void(0)"
              class="nav-link"
              [class.active]="selectedGroupName === null"
              (click)="selectGroup(null)"
            >
              {{ 'NotificationCenter::Inbox:All' | abpLocalization }}
              @if (totalUnreadCount > 0) {
                <span class="badge rounded-pill bg-primary ms-1">{{ totalUnreadCount }}</span>
              }
            </a>
          </li>
          @for (group of groups; track group.name) {
            <li class="nav-item">
              <a
                href="javascript:void(0)"
                class="nav-link"
                [class.active]="selectedGroupName === group.name"
                (click)="selectGroup(group.name ?? null)"
              >
                {{ group.displayName }}
                @if ((group.unreadCount ?? 0) > 0) {
                  <span class="badge rounded-pill bg-primary ms-1">{{ group.unreadCount }}</span>
                }
              </a>
            </li>
          }
        </ul>
      </div>
      <div class="card-body">
        <div class="d-flex flex-wrap justify-content-between align-items-center gap-2 mb-3">
          <div class="btn-group btn-group-sm" role="group">
            <button type="button" class="btn btn-outline-secondary" [class.active]="!unreadOnly" (click)="setUnreadOnly(false)">
              {{ 'NotificationCenter::Inbox:All' | abpLocalization }}
            </button>
            <button type="button" class="btn btn-outline-secondary" [class.active]="unreadOnly" (click)="setUnreadOnly(true)">
              {{ 'NotificationCenter::Inbox:Unread' | abpLocalization }}
            </button>
          </div>
          <div class="d-flex flex-wrap gap-2">
            <button
              type="button"
              class="btn btn-sm btn-outline-primary"
              [disabled]="totalUnreadCount === 0"
              (click)="markAllAsRead()"
            >
              {{ 'NotificationCenter::MarkAllAsRead' | abpLocalization }}
            </button>
            <button type="button" class="btn btn-sm btn-outline-danger" (click)="deleteAllRead()">
              {{ 'NotificationCenter::DeleteAllRead' | abpLocalization }}
            </button>
          </div>
        </div>

        @if (notifications.length === 0) {
          <p class="text-muted mb-0">{{ 'NotificationCenter::NoNotifications' | abpLocalization }}</p>
        } @else {
          <div class="list-group mb-3">
            @for (n of notifications; track n.id) {
              <div class="list-group-item abp-notification-inbox-item" [class.abp-notification-inbox-unread]="n.state === 0">
                <div class="d-flex align-items-start gap-2">
                  <div
                    class="flex-grow-1 abp-notification-inbox-item-body"
                    role="button"
                    tabindex="0"
                    (click)="onNotificationClick(n)"
                    (keydown.enter)="onNotificationClick(n)"
                    (keydown.space)="onNotificationClick(n); $event.preventDefault()"
                  >
                    <div class="abp-notification-inbox-item-header">
                      <span [class]="titleClassOf(n)">{{ n.notificationDisplayName || n.notificationName }}</span>
                      <small class="text-muted text-nowrap">{{ n.creationTime | date: 'short' }}</small>
                    </div>
                    @if (selectedGroupName === null && n.groupDisplayName) {
                      <small class="text-muted d-block">{{ n.groupDisplayName }}</small>
                    }
                    @if (dataComponentOf(n); as dataComponent) {
                      <ng-container [ngComponentOutlet]="dataComponent" [ngComponentOutletInputs]="{ data: n.data }" />
                    } @else if (imageUrlOf(n); as img) {
                      <img class="abp-notification-inbox-item-image" [src]="img" alt="" />
                    }
                  </div>
                  <button
                    type="button"
                    class="btn btn-sm btn-link text-danger p-0"
                    [attr.title]="'NotificationCenter::Delete' | abpLocalization"
                    [attr.aria-label]="'NotificationCenter::Delete' | abpLocalization"
                    (click)="delete(n)"
                  >
                    <i class="fa fa-trash" aria-hidden="true"></i>
                  </button>
                </div>
              </div>
            }
          </div>
          @if (totalCount > pageSize) {
            <ngb-pagination
              [collectionSize]="totalCount"
              [pageSize]="pageSize"
              [page]="page"
              [maxSize]="5"
              [boundaryLinks]="true"
              (pageChange)="onPageChange($event)"
            />
          }
        }
      </div>
    </div>
  `,
  styles: [
    `
      .abp-notification-inbox-item.abp-notification-inbox-unread { background: rgba(13, 110, 253, 0.05); box-shadow: inset 3px 0 0 var(--bs-primary, #0d6efd); }
      .abp-notification-inbox-item-body { min-width: 0; cursor: pointer; }
      .abp-notification-inbox-item-header { display: grid; grid-template-columns: minmax(0, 1fr) max-content; align-items: baseline; gap: 8px; }
      .abp-notification-inbox-item-title { min-width: 0; overflow-wrap: anywhere; font-weight: 600; }
      .abp-notification-inbox-item-image { max-width: 100%; border-radius: 4px; margin-top: 4px; }
    `,
  ],
})
export class NotificationInboxComponent implements OnInit {
  readonly pageSize = 20;

  groups: UserNotificationGroupDto[] = [];
  notifications: UserNotificationDto[] = [];
  totalCount = 0;
  page = 1;
  selectedGroupName: string | null = null;
  unreadOnly = false;

  private markingNotificationIds = new Set<string>();
  private notificationService = inject(UserNotificationService);
  private notificationDataComponents = inject(NotificationDataComponentsService);
  private notificationEntityLinks = inject(NotificationEntityLinksService);
  private navigation = inject(NotificationNavigationService);
  private events = inject(NotificationCenterEventsService);
  private confirmation = inject(ConfirmationService);
  private changeDetectorRef = inject(ChangeDetectorRef);

  /** Unread notifications across every group (the "All" tab's count). */
  get totalUnreadCount(): number {
    return this.groups.reduce((sum, group) => sum + (group.unreadCount ?? 0), 0);
  }

  ngOnInit(): void {
    this.loadGroups();
    this.loadList();
  }

  selectGroup(groupName: string | null): void {
    this.selectedGroupName = groupName;
    this.page = 1;
    this.loadList();
  }

  setUnreadOnly(unreadOnly: boolean): void {
    this.unreadOnly = unreadOnly;
    this.page = 1;
    this.loadList();
  }

  onPageChange(page: number): void {
    this.page = page;
    this.loadList();
  }

  onNotificationClick(n: UserNotificationDto): void {
    const target = this.notificationEntityLinks.resolve(n);
    if (!n.notificationId || n.state === UserNotificationState.Read) {
      this.navigation.navigate(target);
      return;
    }

    const notificationId = n.notificationId;
    if (this.markingNotificationIds.has(notificationId)) {
      return;
    }

    this.markingNotificationIds.add(notificationId);
    this.notificationService.markAsRead(notificationId).subscribe({
      next: () => {
        this.markingNotificationIds.delete(notificationId);
        n.state = UserNotificationState.Read;
        const group = this.groups.find(g => g.name === n.groupName);
        if (group) {
          group.unreadCount = Math.max(0, (group.unreadCount ?? 0) - 1);
        }
        this.events.notifyInboxChanged();
        this.changeDetectorRef.markForCheck();
        this.navigation.navigate(target);
      },
      error: () => {
        this.markingNotificationIds.delete(notificationId);
        this.navigation.navigate(target);
      },
    });
  }

  delete(n: UserNotificationDto): void {
    if (!n.notificationId) {
      return;
    }
    const notificationId = n.notificationId;
    this.confirmThen('NotificationCenter::DeleteNotificationConfirmation', () =>
      this.notificationService.delete(notificationId),
    );
  }

  markAllAsRead(): void {
    this.confirmThen('NotificationCenter::MarkAllAsReadConfirmation', () => this.notificationService.markAllAsRead());
  }

  deleteAllRead(): void {
    this.confirmThen('NotificationCenter::DeleteAllReadConfirmation', () => this.notificationService.deleteAllRead());
  }

  /** Duck-typed image URL (mirrors IHasNotificationImageUrl's "imageUrl" JSON property). */
  imageUrlOf(n: UserNotificationDto): string | null {
    const url = (n.data as NotificationDataPayload | null | undefined)?.['imageUrl'];
    return typeof url === 'string' ? url : null;
  }

  /** The renderer registered for this item's discriminator, or null to fall back to imageUrlOf(). */
  dataComponentOf(n: UserNotificationDto): Type<unknown> | null {
    return this.notificationDataComponents.get((n.data as NotificationDataPayload | null | undefined)?.type);
  }

  titleClassOf(n: UserNotificationDto): string {
    switch (n.severity) {
      case NotificationSeverity.Success:
        return 'abp-notification-inbox-item-title text-success';
      case NotificationSeverity.Warn:
        return 'abp-notification-inbox-item-title text-warning';
      case NotificationSeverity.Error:
        return 'abp-notification-inbox-item-title text-danger';
      default:
        return 'abp-notification-inbox-item-title text-info';
    }
  }

  private confirmThen(message: string, action: () => Observable<void>): void {
    this.confirmation.warn(message, 'AbpUi::AreYouSure').subscribe(status => {
      if (status !== Confirmation.Status.confirm) {
        return;
      }
      action().subscribe(() => {
        this.events.notifyInboxChanged();
        this.loadGroups();
        this.loadList();
      });
    });
  }

  private loadGroups(): void {
    this.notificationService.getGroups().subscribe(result => {
      this.groups = result.items ?? [];
      this.changeDetectorRef.markForCheck();
    });
  }

  private loadList(): void {
    this.notificationService
      .getList({
        groupName: this.selectedGroupName ?? undefined,
        state: this.unreadOnly ? UserNotificationState.Unread : undefined,
        skipCount: (this.page - 1) * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe(result => {
        this.notifications = result.items ?? [];
        this.totalCount = result.totalCount ?? 0;

        // A delete can empty the last page; step back to the new last page instead of showing nothing.
        const lastPage = Math.max(1, Math.ceil(this.totalCount / this.pageSize));
        if (this.notifications.length === 0 && this.page > lastPage) {
          this.page = lastPage;
          this.loadList();
          return;
        }

        this.changeDetectorRef.markForCheck();
      });
  }
}
