import { Component, Input, inject } from '@angular/core';
import { LocalizationService } from '@abp/ng.core';
import { NotificationDataPayload } from './notification-data-payload';

/**
 * Renders a "Dignite.LocalizableMessage" notification, localized client-side (by resource + key +
 * positional arguments) for the current reader's culture. Mirrors
 * LocalizableMessageNotificationDataViewComponent, which does the same resolution server-side.
 */
@Component({
  selector: 'abp-localizable-message-notification-data',
  standalone: true,
  template: `<p class="abp-notification-item-message">{{ text }}</p>`,
})
export class LocalizableMessageNotificationDataComponent {
  private localization = inject(LocalizationService);

  text = '';

  @Input() set data(value: NotificationDataPayload | null | undefined) {
    const resourceName = value?.['resourceName'] as string | undefined;
    const name = value?.['name'] as string | undefined;
    if (!name) {
      this.text = '';
      return;
    }

    const args = Object.values((value?.['arguments'] as Record<string, unknown>) ?? {}).map(String);
    const key = resourceName ? `${resourceName}::${name}` : name;
    this.text = this.localization.instant(key, ...args);
  }
}
