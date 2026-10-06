import { Component, inject } from '@angular/core';
import { RestService } from '@abp/ng.core';
import { RouterLink } from '@angular/router';

/**
 * Demo publisher shown on the home page: drives the @dignite/ng.notification-center library against the host API.
 * The toolbar bell is registered globally by provideNotificationCenterConfig() and refreshes from SignalR.
 * The "Send order shipped notification" button publishes explicitly to the current user (bypasses
 * subscriptions, always arrives) with the host's Demo.OrderShipped payload so the bell uses a custom
 * NotificationData renderer; "Publish to subscribers" demonstrates the actual point of subscriptions — it
 * omits userIds, so DefaultNotificationDistributor resolves recipients from NotificationSubscription rows
 * instead, and only arrives if you're currently subscribed to "Announcement" on the Settings page's
 * Subscriptions tab.
 */
@Component({
  selector: 'app-demo-notifications',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="card mb-3">
      <div class="card-body">
      <h4 class="mb-3">Notification Center demo</h4>
      <p class="text-muted">
        The toolbar bell shows your unread count and recent notifications, updated in real time over SignalR.
        Click below to publish an order notification to yourself and watch the toolbar bell render its custom
        notification template instantly. Open <a routerLink="/notifications">the inbox page</a> to browse
        everything you received, grouped by notification group.
      </p>
      <button type="button" class="btn btn-primary" (click)="publishOrderShipped()">
        Send order shipped notification
      </button>
      <hr />
      <h4>Subscriptions</h4>
      <p class="text-muted">
        Subscriptions let a notification be published without naming recipients up front — only users
        subscribed to that notification type receive it. Toggle "Announcement" on the
        <a routerLink="/setting-management">Settings</a> page's Subscriptions tab, click "Publish to
        subscribers" below, and confirm it only arrives while you're subscribed.
      </p>
      <button type="button" class="btn btn-outline-primary" (click)="publishToSubscribers()">
        Publish to subscribers
      </button>
      @if (publishToSubscribersResult) {
        <span class="ms-2 text-muted">{{ publishToSubscribersResult }}</span>
      }
      </div>
    </div>
  `,
})
export class DemoNotificationsComponent {
  publishToSubscribersResult = '';

  private restService = inject(RestService);

  publishOrderShipped(): void {
    this.restService
      .request<void, void>(
        { method: 'POST', url: '/api/app/demo-notification/publish-order-shipped' },
        { apiName: 'default' },
      )
      .subscribe();
  }

  publishToSubscribers(): void {
    this.publishToSubscribersResult = '';
    this.restService
      .request<void, void>(
        { method: 'POST', url: '/api/app/demo-notification/publish-announcement-to-subscribers' },
        { apiName: 'default' },
      )
      .subscribe(() => {
        this.publishToSubscribersResult =
          'Published — check the bell. It only arrives if you were subscribed to "Announcement" above.';
      });
  }
}
