import { SettingTabsService } from '@abp/ng.setting-management/config';
import { inject, provideAppInitializer } from '@angular/core';
import { NotificationSubscriptionsComponent } from '@dignite/ng.notification-center';

/**
 * Adds a "Subscriptions" tab to the shared Settings page (see
 * https://abp.io/docs/latest/modules/setting-management) instead of a standalone route/menu item —
 * mirrors the MVC UI's NotificationCenterSettingPageContributor.
 */
export const NOTIFICATION_CENTER_SETTING_TAB_PROVIDERS = [
  provideAppInitializer(() => {
    configureSettingTabs();
  }),
];

export function configureSettingTabs() {
  const settingTabs = inject(SettingTabsService);

  settingTabs.add([
    {
      name: 'NotificationCenter::Subscriptions',
      order: 100,
      component: NotificationSubscriptionsComponent,
    },
  ]);
}
