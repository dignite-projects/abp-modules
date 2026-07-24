import { TestBed } from '@angular/core/testing';
import { NotificationDataComponentsService } from './notification-data-components.service';
import { UnsupportedNotificationDataComponent } from './unsupported-notification-data.component';

describe('NotificationDataComponentsService', () => {
  it('routes the stable unsupported discriminator to the safe fallback renderer', () => {
    const service = TestBed.inject(NotificationDataComponentsService);

    expect(service.get('Dignite.Unsupported')).toBe(UnsupportedNotificationDataComponent);
  });
});
