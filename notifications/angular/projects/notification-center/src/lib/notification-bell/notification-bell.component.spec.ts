import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { AuthService, EnvironmentService } from '@abp/ng.core';
import { of, Subject, throwError } from 'rxjs';
import { UserNotificationService, UserNotificationDto } from '../proxy/dignite/abp/notification-center';
import { NotificationSeverity, UserNotificationState } from '../proxy/dignite/abp/notifications';
import { NotificationDataComponentsService } from '../notification-data/notification-data-components.service';
import { NotificationEntityLinksService } from '../notification-links/notification-entity-links.service';
import { NotificationBellComponent } from './notification-bell.component';

// `data` is typed as the strict server DTO shape, but at runtime it's the loose, discriminator-keyed
// payload (see NotificationDataPayload) that dataComponentOf()/imageUrlOf() actually read - accept
// `unknown` here rather than fighting the DTO type with a cast at every call site.
function notification(
  overrides: Partial<Omit<UserNotificationDto, 'data'>> & { data?: unknown } = {},
): UserNotificationDto {
  return {
    id: '1',
    notificationId: 'n1',
    notificationName: 'order.shipped',
    state: UserNotificationState.Unread,
    ...overrides,
  } as UserNotificationDto;
}

describe('NotificationBellComponent', () => {
  let notificationService: {
    getUnreadCount: ReturnType<typeof vi.fn>;
    getList: ReturnType<typeof vi.fn>;
    markAsRead: ReturnType<typeof vi.fn>;
    markAllAsRead: ReturnType<typeof vi.fn>;
    apiName: string;
  };
  let dataComponents: { get: ReturnType<typeof vi.fn> };
  let entityLinks: { resolve: ReturnType<typeof vi.fn> };
  let authService: { isAuthenticated: boolean; getAccessToken: ReturnType<typeof vi.fn> };
  let environmentService: { getApiUrl: ReturnType<typeof vi.fn> };
  let router: { navigate: ReturnType<typeof vi.fn>; navigateByUrl: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    notificationService = {
      getUnreadCount: vi.fn(() => of(0)),
      getList: vi.fn(() => of({ items: [], totalCount: 0 })),
      markAsRead: vi.fn(() => of(undefined)),
      markAllAsRead: vi.fn(() => of(undefined)),
      apiName: 'NotificationCenter',
    };
    dataComponents = { get: vi.fn(() => null) };
    entityLinks = { resolve: vi.fn(() => null) };
    // Kept unauthenticated by default in every test: startRealtime() then no-ops, so no test ever
    // triggers a real SignalR HubConnection/network call. The authenticated path is out of scope here.
    authService = { isAuthenticated: false, getAccessToken: vi.fn(() => 'token') };
    environmentService = { getApiUrl: vi.fn(() => 'https://api.example.com') };
    router = { navigate: vi.fn(() => Promise.resolve(true)), navigateByUrl: vi.fn(() => Promise.resolve(true)) };
  });

  function render() {
    // The real template pulls in NgbDropdownModule/NgComponentOutlet, neither relevant to what's
    // under test here - every test drives the component's own methods directly. DOCUMENT is left as
    // Angular's real one: it's also what the test renderer itself uses to manage the root fixture
    // element, so a bare-object stub for it breaks TestBed, not just the component under test.
    TestBed.overrideComponent(NotificationBellComponent, { set: { template: '', imports: [] } });
    TestBed.configureTestingModule({
      providers: [
        { provide: UserNotificationService, useValue: notificationService },
        { provide: NotificationDataComponentsService, useValue: dataComponents },
        { provide: NotificationEntityLinksService, useValue: entityLinks },
        { provide: AuthService, useValue: authService },
        { provide: EnvironmentService, useValue: environmentService },
        { provide: Router, useValue: router },
      ],
    });
    const fixture = TestBed.createComponent(NotificationBellComponent);
    fixture.detectChanges();
    return fixture;
  }

  describe('refresh', () => {
    it('loads the unread count and the unread list on init', () => {
      notificationService.getUnreadCount = vi.fn(() => of(3));
      notificationService.getList = vi.fn(() => of({ items: [notification()], totalCount: 1 }));

      const fixture = render();

      expect(fixture.componentInstance.unreadCount).toBe(3);
      expect(fixture.componentInstance.notifications).toHaveLength(1);
      expect(notificationService.getList).toHaveBeenCalledWith({
        state: UserNotificationState.Unread,
        maxResultCount: 10,
      });
    });

    it('refreshes again when the dropdown opens, but not when it closes', () => {
      const fixture = render();
      notificationService.getUnreadCount.mockClear();

      fixture.componentInstance.onDropdownOpenChange(true);
      expect(notificationService.getUnreadCount).toHaveBeenCalledTimes(1);

      fixture.componentInstance.onDropdownOpenChange(false);
      expect(notificationService.getUnreadCount).toHaveBeenCalledTimes(1);
    });
  });

  describe('markAsRead', () => {
    it('marks an unread notification read, decrements the badge, and navigates', () => {
      const fixture = render();
      fixture.componentInstance.unreadCount = 1;
      const n = notification();

      fixture.componentInstance.markAsRead(n, '/orders/1');

      expect(notificationService.markAsRead).toHaveBeenCalledWith('n1');
      expect(n.state).toBe(UserNotificationState.Read);
      expect(fixture.componentInstance.unreadCount).toBe(0);
      expect(router.navigateByUrl).toHaveBeenCalledWith('/orders/1');
    });

    it('never lets the unread count go negative', () => {
      const fixture = render();
      fixture.componentInstance.unreadCount = 0;

      fixture.componentInstance.markAsRead(notification());

      expect(fixture.componentInstance.unreadCount).toBe(0);
    });

    it('navigates directly without calling the service for an already-read notification', () => {
      const fixture = render();

      fixture.componentInstance.markAsRead(notification({ state: UserNotificationState.Read }), '/orders/1');

      expect(notificationService.markAsRead).not.toHaveBeenCalled();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/orders/1');
    });

    it('navigates directly when the notification has no notificationId', () => {
      const fixture = render();

      fixture.componentInstance.markAsRead(notification({ notificationId: undefined }), '/orders/1');

      expect(notificationService.markAsRead).not.toHaveBeenCalled();
      expect(router.navigateByUrl).toHaveBeenCalledWith('/orders/1');
    });

    it('does not call the service twice for the same notification while the first call is still in flight', () => {
      const pending = new Subject<void>();
      notificationService.markAsRead = vi.fn(() => pending.asObservable());
      const fixture = render();
      const n = notification();

      fixture.componentInstance.markAsRead(n);
      fixture.componentInstance.markAsRead(n);

      expect(notificationService.markAsRead).toHaveBeenCalledTimes(1);

      pending.next();
      pending.complete();
    });

    it('still cleans up and navigates when the mark-as-read call fails', () => {
      notificationService.markAsRead = vi.fn(() => throwError(() => new Error('network error')));
      const fixture = render();

      fixture.componentInstance.markAsRead(notification(), '/orders/1');

      expect(router.navigateByUrl).toHaveBeenCalledWith('/orders/1');
    });
  });

  describe('markAllAsRead', () => {
    it('marks every notification read locally and resets the badge', () => {
      const fixture = render();
      fixture.componentInstance.notifications = [
        notification({ id: '1', state: UserNotificationState.Unread }),
        notification({ id: '2', state: UserNotificationState.Unread }),
      ];
      fixture.componentInstance.unreadCount = 2;

      fixture.componentInstance.markAllAsRead();

      expect(fixture.componentInstance.notifications.every(n => n.state === UserNotificationState.Read)).toBe(true);
      expect(fixture.componentInstance.unreadCount).toBe(0);
    });
  });

  describe('imageUrlOf', () => {
    it('reads a string imageUrl off the payload', () => {
      const fixture = render();
      const n = notification({ data: { imageUrl: 'https://img/x.png' } });

      expect(fixture.componentInstance.imageUrlOf(n)).toBe('https://img/x.png');
    });

    it('returns null when imageUrl is missing, not a string, or there is no data', () => {
      const fixture = render();

      expect(fixture.componentInstance.imageUrlOf(notification({ data: {} }))).toBeNull();
      expect(fixture.componentInstance.imageUrlOf(notification({ data: { imageUrl: 42 } }))).toBeNull();
      expect(fixture.componentInstance.imageUrlOf(notification({ data: null }))).toBeNull();
    });
  });

  describe('dataComponentOf', () => {
    it('delegates to the registry by the payload discriminator', () => {
      const fixture = render();
      const n = notification({ data: { type: 'Dignite.Message' } });

      fixture.componentInstance.dataComponentOf(n);

      expect(dataComponents.get).toHaveBeenCalledWith('Dignite.Message');
    });
  });

  describe('notificationTitleClassOf', () => {
    it.each([
      [NotificationSeverity.Success, 'text-success'],
      [NotificationSeverity.Warn, 'text-warning'],
      [NotificationSeverity.Error, 'text-danger'],
      [NotificationSeverity.Info, 'text-info'],
      [undefined, 'text-info'],
    ])('maps severity %s to %s', (severity, expectedClass) => {
      const fixture = render();

      expect(fixture.componentInstance.notificationTitleClassOf(notification({ severity }))).toContain(
        expectedClass,
      );
    });
  });

  describe('navigation targets (via onNotificationClick)', () => {
    // jsdom's Location.assign is non-configurable, so it can't be spied on in place - swap the whole
    // `window.location` property for a minimal stub instead, and put the real one back afterward.
    const realLocation = window.location;
    let assignSpy: ReturnType<typeof vi.fn>;

    beforeEach(() => {
      assignSpy = vi.fn();
      Object.defineProperty(window, 'location', {
        value: { origin: realLocation.origin, assign: assignSpy },
        configurable: true,
        writable: true,
      });
    });

    afterEach(() => {
      Object.defineProperty(window, 'location', { value: realLocation, configurable: true, writable: true });
    });

    it('does not navigate when there is no resolvable target', () => {
      entityLinks.resolve = vi.fn(() => null);
      const fixture = render();

      fixture.componentInstance.onNotificationClick(notification({ state: UserNotificationState.Read }));

      expect(router.navigate).not.toHaveBeenCalled();
      expect(router.navigateByUrl).not.toHaveBeenCalled();
    });

    it('navigates with the router for an array target', () => {
      entityLinks.resolve = vi.fn(() => ['/orders', '42']);
      const fixture = render();

      fixture.componentInstance.onNotificationClick(notification({ state: UserNotificationState.Read }));

      expect(router.navigate).toHaveBeenCalledWith(['/orders', '42']);
    });

    it('navigates with the router for a relative string target', () => {
      entityLinks.resolve = vi.fn(() => '/orders/42');
      const fixture = render();

      fixture.componentInstance.onNotificationClick(notification({ state: UserNotificationState.Read }));

      expect(router.navigateByUrl).toHaveBeenCalledWith('/orders/42');
    });

    it('navigates with the router for a same-origin absolute URL, using only the path', () => {
      entityLinks.resolve = vi.fn(() => `${window.location.origin}/orders/42?x=1#y`);
      const fixture = render();

      fixture.componentInstance.onNotificationClick(notification({ state: UserNotificationState.Read }));

      expect(router.navigateByUrl).toHaveBeenCalledWith('/orders/42?x=1#y');
      expect(assignSpy).not.toHaveBeenCalled();
    });

    it('leaves the SPA for a cross-origin URL instead of routing to it', () => {
      entityLinks.resolve = vi.fn(() => 'https://evil.example.com/phish');
      const fixture = render();

      fixture.componentInstance.onNotificationClick(notification({ state: UserNotificationState.Read }));

      expect(assignSpy).toHaveBeenCalledWith('https://evil.example.com/phish');
      expect(router.navigateByUrl).not.toHaveBeenCalled();
    });

    it('treats a protocol-relative URL as external', () => {
      entityLinks.resolve = vi.fn(() => '//evil.example.com/phish');
      const fixture = render();

      fixture.componentInstance.onNotificationClick(notification({ state: UserNotificationState.Read }));

      expect(assignSpy).toHaveBeenCalledWith('//evil.example.com/phish');
    });

    it('ignores a blank string target', () => {
      entityLinks.resolve = vi.fn(() => '   ');
      const fixture = render();

      fixture.componentInstance.onNotificationClick(notification({ state: UserNotificationState.Read }));

      expect(router.navigate).not.toHaveBeenCalled();
      expect(router.navigateByUrl).not.toHaveBeenCalled();
      expect(assignSpy).not.toHaveBeenCalled();
    });

    it('navigates with the router for a UrlTree-like target', () => {
      const urlTree = {} as UrlTree;
      entityLinks.resolve = vi.fn(() => urlTree);
      const fixture = render();

      fixture.componentInstance.onNotificationClick(notification({ state: UserNotificationState.Read }));

      expect(router.navigateByUrl).toHaveBeenCalledWith(urlTree);
    });
  });

  describe('startRealtime', () => {
    it('does not attempt a hub connection when the user is not authenticated', () => {
      authService.isAuthenticated = false;
      const fixture = render();

      expect((fixture.componentInstance as unknown as { hubConnection?: unknown }).hubConnection).toBeUndefined();
    });

    it('tears down cleanly even when no hub connection was ever started', () => {
      const fixture = render();

      expect(() => fixture.componentInstance.ngOnDestroy()).not.toThrow();
    });
  });

  describe('smallScreen', () => {
    const originalInnerWidth = window.innerWidth;

    afterEach(() => {
      Object.defineProperty(window, 'innerWidth', { value: originalInnerWidth, configurable: true });
    });

    it('is true under the responsive breakpoint', () => {
      Object.defineProperty(window, 'innerWidth', { value: 500, configurable: true });
      const fixture = render();

      expect(fixture.componentInstance.smallScreen).toBe(true);
    });

    it('is false at or above the responsive breakpoint', () => {
      Object.defineProperty(window, 'innerWidth', { value: 992, configurable: true });
      const fixture = render();

      expect(fixture.componentInstance.smallScreen).toBe(false);
    });
  });
});
