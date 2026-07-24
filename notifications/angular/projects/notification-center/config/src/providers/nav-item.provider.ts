import { AuthService } from '@abp/ng.core';
import { NavItemsService } from '@abp/ng.theme.shared';
import { inject, provideAppInitializer } from '@angular/core';
import { NotificationBellComponent } from '@dignite/ng.notification-center';

/**
 * Puts the notification bell into the host's toolbar/navbar on every page (via LeptonX's
 * NavItemsService — the same extension point the theme itself uses for the language selector and
 * current-user menu), mirroring the MVC UI's NotificationCenterToolbarContributor.
 */
export const NOTIFICATION_CENTER_NAV_ITEM_PROVIDERS = [
  provideAppInitializer(() => {
    configureNavItems();
  }),
];

export function configureNavItems() {
  const navItems = inject(NavItemsService);
  const authService = inject(AuthService);

  navItems.addItems([
    {
      id: 'NotificationCenter.NotificationBell',
      order: 100,
      visible: () => authService.isAuthenticated,
      component: NotificationBellComponent,
    },
  ]);
}
