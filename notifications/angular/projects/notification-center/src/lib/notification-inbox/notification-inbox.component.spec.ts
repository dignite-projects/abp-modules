import { TestBed } from '@angular/core/testing';
import { Confirmation, ConfirmationService } from '@abp/ng.theme.shared';
import { Subject, of } from 'rxjs';
import { UserNotificationDto, UserNotificationService } from '../proxy/dignite/abp/notification-center';
import { UserNotificationState } from '../proxy/dignite/abp/notifications';
import { NotificationEntityLinksService } from '../notification-links/notification-entity-links.service';
import { NotificationNavigationService } from '../notification-links/notification-navigation.service';
import { NotificationCenterEventsService } from './notification-center-events.service';
import { NotificationInboxComponent } from './notification-inbox.component';

describe('NotificationInboxComponent', () => {
  let notificationService: {
    getGroups: ReturnType<typeof vi.fn>;
    getList: ReturnType<typeof vi.fn>;
    markAsRead: ReturnType<typeof vi.fn>;
    markAllAsRead: ReturnType<typeof vi.fn>;
    delete: ReturnType<typeof vi.fn>;
    deleteAllRead: ReturnType<typeof vi.fn>;
  };
  let confirmation: { warn: ReturnType<typeof vi.fn> };
  let entityLinks: { resolve: ReturnType<typeof vi.fn> };
  let navigation: { navigate: ReturnType<typeof vi.fn> };
  let events: NotificationCenterEventsService;

  const groups = () => [
    { name: 'Orders', displayName: 'Orders', unreadCount: 2 },
    { name: 'System', displayName: 'System', unreadCount: 1 },
  ];

  const notification = (overrides: Partial<UserNotificationDto> = {}): UserNotificationDto => ({
    id: 'row-1',
    notificationId: 'n-1',
    notificationName: 'order.shipped',
    groupName: 'Orders',
    state: UserNotificationState.Unread,
    ...overrides,
  });

  beforeEach(() => {
    notificationService = {
      getGroups: vi.fn(() => of({ items: groups() })),
      getList: vi.fn(() => of({ items: [notification()], totalCount: 1 })),
      markAsRead: vi.fn(() => of(undefined)),
      markAllAsRead: vi.fn(() => of(undefined)),
      delete: vi.fn(() => of(undefined)),
      deleteAllRead: vi.fn(() => of(undefined)),
    };
    confirmation = { warn: vi.fn(() => of(Confirmation.Status.confirm)) };
    entityLinks = { resolve: vi.fn(() => '/orders/1') };
    navigation = { navigate: vi.fn() };

    TestBed.overrideComponent(NotificationInboxComponent, { set: { template: '', imports: [] } });
    TestBed.configureTestingModule({
      providers: [
        { provide: UserNotificationService, useValue: notificationService },
        { provide: ConfirmationService, useValue: confirmation },
        { provide: NotificationEntityLinksService, useValue: entityLinks },
        { provide: NotificationNavigationService, useValue: navigation },
      ],
    });
    events = TestBed.inject(NotificationCenterEventsService);
  });

  function render() {
    const fixture = TestBed.createComponent(NotificationInboxComponent);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('loads the groups and the first page of every group on init', () => {
    const component = render();

    expect(notificationService.getList).toHaveBeenCalledWith({
      groupName: undefined,
      state: undefined,
      skipCount: 0,
      maxResultCount: 20,
    });
    expect(component.groups.map(group => group.name)).toEqual(['Orders', 'System']);
    expect(component.totalUnreadCount).toBe(3);
    expect(component.notifications.length).toBe(1);
  });

  it('filters by group and unread state, resetting to the first page', () => {
    const component = render();
    component.page = 3;

    component.selectGroup('System');
    expect(notificationService.getList).toHaveBeenLastCalledWith(
      expect.objectContaining({ groupName: 'System', state: undefined, skipCount: 0 }),
    );

    component.setUnreadOnly(true);
    expect(notificationService.getList).toHaveBeenLastCalledWith(
      expect.objectContaining({ groupName: 'System', state: UserNotificationState.Unread, skipCount: 0 }),
    );
  });

  it('pages through the list', () => {
    const component = render();

    component.onPageChange(2);

    expect(notificationService.getList).toHaveBeenLastCalledWith(
      expect.objectContaining({ skipCount: 20, maxResultCount: 20 }),
    );
  });

  it('marks an unread item read in place, decrements its group, notifies the bell, and navigates', () => {
    const component = render();
    const inboxChanged = vi.fn();
    events.inboxChanged$.subscribe(inboxChanged);
    const item = component.notifications[0];

    component.onNotificationClick(item);

    expect(notificationService.markAsRead).toHaveBeenCalledWith('n-1');
    expect(item.state).toBe(UserNotificationState.Read);
    expect(component.notifications).toContain(item);
    expect(component.groups.find(group => group.name === 'Orders')?.unreadCount).toBe(1);
    expect(component.totalUnreadCount).toBe(2);
    expect(inboxChanged).toHaveBeenCalledTimes(1);
    expect(navigation.navigate).toHaveBeenCalledWith('/orders/1');
  });

  it('navigates a read item without calling the service', () => {
    const component = render();

    component.onNotificationClick(notification({ state: UserNotificationState.Read }));

    expect(notificationService.markAsRead).not.toHaveBeenCalled();
    expect(navigation.navigate).toHaveBeenCalledWith('/orders/1');
  });

  it('deletes after confirmation, then reloads the groups and the list and notifies the bell', () => {
    const component = render();
    const inboxChanged = vi.fn();
    events.inboxChanged$.subscribe(inboxChanged);
    notificationService.getGroups.mockClear();
    notificationService.getList.mockClear();

    component.delete(component.notifications[0]);

    expect(confirmation.warn).toHaveBeenCalledWith(
      'NotificationCenter::DeleteNotificationConfirmation',
      'AbpUi::AreYouSure',
    );
    expect(notificationService.delete).toHaveBeenCalledWith('n-1');
    expect(notificationService.getGroups).toHaveBeenCalledTimes(1);
    expect(notificationService.getList).toHaveBeenCalledTimes(1);
    expect(inboxChanged).toHaveBeenCalledTimes(1);
  });

  it('does nothing when a confirmation is rejected', () => {
    confirmation.warn = vi.fn(() => of(Confirmation.Status.reject));
    const component = render();

    component.delete(component.notifications[0]);
    component.markAllAsRead();
    component.deleteAllRead();

    expect(notificationService.delete).not.toHaveBeenCalled();
    expect(notificationService.markAllAsRead).not.toHaveBeenCalled();
    expect(notificationService.deleteAllRead).not.toHaveBeenCalled();
  });

  it('runs the bulk actions after confirmation', () => {
    const component = render();

    component.markAllAsRead();
    component.deleteAllRead();

    expect(confirmation.warn).toHaveBeenCalledWith('NotificationCenter::MarkAllAsReadConfirmation', 'AbpUi::AreYouSure');
    expect(confirmation.warn).toHaveBeenCalledWith('NotificationCenter::DeleteAllReadConfirmation', 'AbpUi::AreYouSure');
    expect(notificationService.markAllAsRead).toHaveBeenCalledTimes(1);
    expect(notificationService.deleteAllRead).toHaveBeenCalledTimes(1);
  });

  it('ignores a slower response for a selection the user already left', () => {
    const component = render();
    const ordersResponse = new Subject<{ items: UserNotificationDto[]; totalCount: number }>();
    const systemResponse = new Subject<{ items: UserNotificationDto[]; totalCount: number }>();
    notificationService.getList.mockReturnValueOnce(ordersResponse).mockReturnValueOnce(systemResponse);

    component.selectGroup('Orders');
    component.selectGroup('System');
    systemResponse.next({ items: [notification({ id: 'system', groupName: 'System' })], totalCount: 1 });
    ordersResponse.next({ items: [notification({ id: 'orders-1' }), notification({ id: 'orders-2' })], totalCount: 2 });

    expect(component.selectedGroupName).toBe('System');
    expect(component.notifications.map(n => n.id)).toEqual(['system']);
    expect(component.totalCount).toBe(1);
  });

  it('steps back to the new last page when a delete empties the current one', () => {
    const component = render();
    component.page = 3;
    notificationService.getList
      .mockReturnValueOnce(of({ items: [], totalCount: 25 }))
      .mockReturnValueOnce(of({ items: [notification()], totalCount: 25 }));

    component.onPageChange(3);

    expect(component.page).toBe(2);
    expect(notificationService.getList).toHaveBeenLastCalledWith(expect.objectContaining({ skipCount: 20 }));
    expect(component.notifications.length).toBe(1);
  });
});
