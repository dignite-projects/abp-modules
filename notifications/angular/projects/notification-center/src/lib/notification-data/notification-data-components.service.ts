import { Injectable, Type } from '@angular/core';
import { MessageNotificationDataComponent } from './message-notification-data.component';
import { LocalizableMessageNotificationDataComponent } from './localizable-message-notification-data.component';
import { UnsupportedNotificationDataComponent } from './unsupported-notification-data.component';

/**
 * Registry of per-discriminator renderers for a notification's `data`, keyed by the stable
 * `[NotificationDataType]` discriminator (never a CLR type name) — mirrors the MVC UI's
 * `NotificationCenterWebOptions.DataViewComponents`. A host app calls `register()` with its own
 * discriminator + standalone component to render its own custom `NotificationData` subclasses;
 * an item with no matching entry falls back to generic image-only rendering. The built-in
 * "Dignite.Unsupported" entry renders a safe message for tolerant server placeholders.
 */
@Injectable({ providedIn: 'root' })
export class NotificationDataComponentsService {
  private readonly components = new Map<string, Type<unknown>>([
    ['Dignite.Message', MessageNotificationDataComponent],
    ['Dignite.LocalizableMessage', LocalizableMessageNotificationDataComponent],
    ['Dignite.Unsupported', UnsupportedNotificationDataComponent],
  ]);

  register(discriminator: string, component: Type<unknown>): void {
    this.components.set(discriminator, component);
  }

  get(discriminator: string | null | undefined): Type<unknown> | null {
    return discriminator ? (this.components.get(discriminator) ?? null) : null;
  }
}
