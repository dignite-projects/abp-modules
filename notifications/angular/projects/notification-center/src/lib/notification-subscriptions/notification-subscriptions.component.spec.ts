import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { Subject, of } from 'rxjs';
import { NotificationSubscriptionService } from '../proxy/dignite/abp/notification-center';
import { NotificationSubscriptionsComponent } from './notification-subscriptions.component';

describe('NotificationSubscriptionsComponent', () => {
  const notificationService = {
    getSubscriptions: vi.fn(() => of({ items: [] })),
    subscribe: vi.fn(() => of(undefined)),
    unsubscribe: vi.fn(() => of(undefined)),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    TestBed.overrideComponent(NotificationSubscriptionsComponent, {
      set: { template: '', imports: [] },
    });
    TestBed.configureTestingModule({
      providers: [{ provide: NotificationSubscriptionService, useValue: notificationService }],
    });
  });

  it('submits the complete entity scope when subscribing', () => {
    const component = TestBed.createComponent(NotificationSubscriptionsComponent).componentInstance;
    const subscription = {
      notificationName: 'order.shipped',
      entityTypeName: 'Demo.Order',
      entityId: '42',
      isSubscribed: false,
    };

    component.toggle(subscription, true);

    expect(notificationService.subscribe).toHaveBeenCalledWith({
      notificationName: 'order.shipped',
      entityTypeName: 'Demo.Order',
      entityId: '42',
    });
    expect(subscription.isSubscribed).toBe(true);
  });

  it('keeps definition-wide and entity rows distinct and unsubscribes the exact scope', () => {
    const component = TestBed.createComponent(NotificationSubscriptionsComponent).componentInstance;
    const definitionWide = {
      notificationName: 'order.shipped',
      entityTypeName: null,
      entityId: null,
      isSubscribed: true,
    };
    const entitySpecific = {
      notificationName: 'order.shipped',
      entityTypeName: 'Demo.Order',
      entityId: '42',
      isSubscribed: true,
    };

    expect(component.scopeKey(definitionWide)).not.toBe(component.scopeKey(entitySpecific));
    component.toggle(entitySpecific, false);

    expect(notificationService.unsubscribe).toHaveBeenCalledWith({
      notificationName: 'order.shipped',
      entityTypeName: 'Demo.Order',
      entityId: '42',
    });
    expect(entitySpecific.isSubscribed).toBe(false);
    expect(definitionWide.isSubscribed).toBe(true);
  });

  it('groups rows by notification group in first-appearance order', () => {
    notificationService.getSubscriptions.mockReturnValueOnce(
      of({
        items: [
          { notificationName: 'order.shipped', groupName: 'Orders', groupDisplayName: 'Orders' },
          { notificationName: 'system.announcement', groupName: 'System', groupDisplayName: 'System' },
          {
            notificationName: 'order.shipped',
            entityTypeName: 'Demo.Order',
            entityId: '42',
            groupName: 'Orders',
            groupDisplayName: 'Orders',
          },
          { notificationName: 'removed', groupName: 'Dignite.NotificationCenter.Other', groupDisplayName: null },
        ],
      }) as any,
    );
    const fixture = TestBed.createComponent(NotificationSubscriptionsComponent);
    fixture.componentInstance.ngOnInit();

    const groups = fixture.componentInstance.groups();
    expect(groups.map(group => group.name)).toEqual(['Orders', 'System', 'Dignite.NotificationCenter.Other']);
    expect(groups[0].subscriptions.map(s => s.entityId ?? null)).toEqual([null, '42']);
    expect(groups[2].displayName).toBe('Dignite.NotificationCenter.Other');
  });
});

/**
 * The component renders as a Settings tab in the host's page flow, under the layout's OnPush ancestors
 * (`abp-router-outlet`, the LeptonX layouts). A plain-field write in the HTTP callback never marks those for
 * check, so the table stayed empty until an unrelated event refreshed the view.
 */
@Component({
  selector: 'abp-test-on-push-ancestor',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NotificationSubscriptionsComponent],
  template: '<abp-notification-subscriptions />',
})
class OnPushAncestorComponent {}

@Component({
  selector: 'abp-test-root',
  imports: [OnPushAncestorComponent],
  template: '<abp-test-on-push-ancestor />',
})
class RootComponent {}

describe('NotificationSubscriptionsComponent under an OnPush ancestor', () => {
  const subscriptions$ = new Subject<{ items: unknown[] }>();

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig()],
      providers: [{ provide: NotificationSubscriptionService, useValue: { getSubscriptions: () => subscriptions$ } }],
    });
  });

  it('renders the rows once the response arrives, with no event to mark the ancestors', () => {
    const fixture = TestBed.createComponent(RootComponent);
    fixture.detectChanges();
    const checkboxes = () => fixture.nativeElement.querySelectorAll('tbody input[type="checkbox"]');
    expect(checkboxes()).toHaveLength(0);

    subscriptions$.next({
      items: [
        { notificationName: 'order.shipped', groupName: 'Orders', groupDisplayName: 'Orders', isSubscribed: true },
        { notificationName: 'system.announcement', groupName: 'System', groupDisplayName: 'System', isSubscribed: false },
      ],
    });
    fixture.detectChanges();

    expect(checkboxes()).toHaveLength(2);
  });
});
