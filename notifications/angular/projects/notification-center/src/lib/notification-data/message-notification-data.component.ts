import { Component, Input } from '@angular/core';
import { NotificationDataPayload } from './notification-data-payload';

/** Renders a "Dignite.Message" notification's pre-formatted text. Mirrors MessageNotificationDataViewComponent. */
@Component({
  selector: 'abp-message-notification-data',
  standalone: true,
  template: `<p class="abp-notification-item-message">{{ data?.['message'] }}</p>`,
})
export class MessageNotificationDataComponent {
  @Input() data?: NotificationDataPayload | null;
}
