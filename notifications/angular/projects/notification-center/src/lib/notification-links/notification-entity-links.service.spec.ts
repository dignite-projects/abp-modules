import { TestBed } from '@angular/core/testing';
import { UserNotificationDto } from '../proxy/dignite/abp/notification-center';
import { NotificationEntityLinksService } from './notification-entity-links.service';

function notification(overrides: Partial<UserNotificationDto> = {}): UserNotificationDto {
  return {
    id: '1',
    notificationName: 'order.shipped',
    entityTypeName: 'Demo.Order',
    entityId: '42',
    ...overrides,
  };
}

describe('NotificationEntityLinksService', () => {
  it('resolves through the resolver registered for the entity type', () => {
    const service = TestBed.inject(NotificationEntityLinksService);
    service.register('Demo.Order', n => ['/orders', n.entityId!]);

    expect(service.resolve(notification())).toEqual(['/orders', '42']);
  });

  it('returns null when nothing was ever registered for the entity type', () => {
    const service = TestBed.inject(NotificationEntityLinksService);

    expect(service.resolve(notification({ entityTypeName: 'Demo.Unregistered' }))).toBeNull();
  });

  it('returns null when the notification has no entity type at all', () => {
    const service = TestBed.inject(NotificationEntityLinksService);
    service.register('Demo.Order', () => '/orders');

    expect(service.resolve(notification({ entityTypeName: null }))).toBeNull();
  });

  it('passes through a resolver that itself declines with null', () => {
    const service = TestBed.inject(NotificationEntityLinksService);
    service.register('Demo.Order', () => null);

    expect(service.resolve(notification())).toBeNull();
  });

  it('uses the last registration when the same entity type is registered twice', () => {
    const service = TestBed.inject(NotificationEntityLinksService);
    service.register('Demo.Order', () => '/first');
    service.register('Demo.Order', () => '/second');

    expect(service.resolve(notification())).toBe('/second');
  });

  it('passes the whole notification to the resolver, not just the entity id', () => {
    const service = TestBed.inject(NotificationEntityLinksService);
    service.register('Demo.Order', n => `/orders/${n.entityId}?notification=${n.notificationName}`);

    expect(service.resolve(notification())).toBe('/orders/42?notification=order.shipped');
  });
});
