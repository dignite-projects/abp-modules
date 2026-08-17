import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { NotificationDataComponentsService } from './notification-data-components.service';
import { MessageNotificationDataComponent } from './message-notification-data.component';
import { LocalizableMessageNotificationDataComponent } from './localizable-message-notification-data.component';
import { UnsupportedNotificationDataComponent } from './unsupported-notification-data.component';

@Component({ selector: 'abp-stub-notification-data', standalone: true, template: '' })
class StubNotificationDataComponent {}

describe('NotificationDataComponentsService', () => {
  it('routes every built-in discriminator to its own renderer', () => {
    const service = TestBed.inject(NotificationDataComponentsService);

    expect(service.get('Dignite.Message')).toBe(MessageNotificationDataComponent);
    expect(service.get('Dignite.LocalizableMessage')).toBe(LocalizableMessageNotificationDataComponent);
    expect(service.get('Dignite.Unsupported')).toBe(UnsupportedNotificationDataComponent);
  });

  it('returns null for a discriminator nothing was ever registered under', () => {
    const service = TestBed.inject(NotificationDataComponentsService);

    expect(service.get('Host.NeverRegistered')).toBeNull();
  });

  it('returns null rather than throwing for a null or undefined discriminator', () => {
    const service = TestBed.inject(NotificationDataComponentsService);

    expect(service.get(null)).toBeNull();
    expect(service.get(undefined)).toBeNull();
  });

  it('lets a host app register a renderer for its own custom NotificationData subclass', () => {
    const service = TestBed.inject(NotificationDataComponentsService);

    service.register('Host.OrderShipped', StubNotificationDataComponent);

    expect(service.get('Host.OrderShipped')).toBe(StubNotificationDataComponent);
  });

  it('lets a host app override a built-in renderer', () => {
    const service = TestBed.inject(NotificationDataComponentsService);

    service.register('Dignite.Message', StubNotificationDataComponent);

    expect(service.get('Dignite.Message')).toBe(StubNotificationDataComponent);
  });
});
