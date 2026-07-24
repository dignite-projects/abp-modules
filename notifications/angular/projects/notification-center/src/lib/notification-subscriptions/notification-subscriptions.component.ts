import { Component, OnInit, inject } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import {
  NotificationSubscriptionService,
  NotificationSubscriptionDto,
  NotificationSubscriptionScopeDto,
} from '../proxy/dignite/abp/notification-center';

/**
 * Lists the notification types available to the current user with subscribe/unsubscribe toggles.
 */
@Component({
  selector: 'abp-notification-subscriptions',
  standalone: true,
  imports: [LocalizationPipe],
  template: `
    <table class="abp-notification-subscriptions-table">
      <thead>
        <tr>
          <th>{{ 'NotificationCenter::NotificationType' | abpLocalization }}</th>
          <th>{{ 'NotificationCenter::SubscriptionScope' | abpLocalization }}</th>
          <th>{{ 'NotificationCenter::Description' | abpLocalization }}</th>
          <th>{{ 'NotificationCenter::Subscribed' | abpLocalization }}</th>
        </tr>
      </thead>
      <tbody>
        @for (s of subscriptions; track scopeKey(s)) {
          <tr>
            <td>{{ s.displayName || s.notificationName }}</td>
            <td>
              @if (s.entityTypeName) {
                <code>{{ s.entityTypeName }} / {{ s.entityId }}</code>
              } @else {
                {{ 'NotificationCenter::AllEntities' | abpLocalization }}
              }
            </td>
            <td>{{ s.description }}</td>
            <td>
              <input
                type="checkbox"
                [checked]="s.isSubscribed"
                (change)="toggle(s, $any($event.target).checked)"
              />
            </td>
          </tr>
        }
      </tbody>
    </table>
  `,
  styles: [
    `
      .abp-notification-subscriptions-table { width: 100%; border-collapse: collapse; }
      .abp-notification-subscriptions-table th, .abp-notification-subscriptions-table td { text-align: left; padding: 6px 10px; border-bottom: 1px solid #eee; }
    `,
  ],
})
export class NotificationSubscriptionsComponent implements OnInit {
  subscriptions: NotificationSubscriptionDto[] = [];

  private notificationService = inject(NotificationSubscriptionService);

  ngOnInit(): void {
    this.notificationService.getSubscriptions().subscribe(r => (this.subscriptions = r.items));
  }

  toggle(subscription: NotificationSubscriptionDto, subscribe: boolean): void {
    const scope = this.toScope(subscription);
    const request = subscribe
      ? this.notificationService.subscribe(scope)
      : this.notificationService.unsubscribe(scope);
    request.subscribe(() => (subscription.isSubscribed = subscribe));
  }

  scopeKey(subscription: NotificationSubscriptionDto): string {
    return `${subscription.notificationName ?? ''}\u0000${subscription.entityTypeName ?? ''}\u0000${subscription.entityId ?? ''}`;
  }

  private toScope(subscription: NotificationSubscriptionDto): NotificationSubscriptionScopeDto {
    return {
      notificationName: subscription.notificationName!,
      entityTypeName: subscription.entityTypeName,
      entityId: subscription.entityId,
    };
  }
}
