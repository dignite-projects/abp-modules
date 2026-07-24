import { makeEnvironmentProviders } from '@angular/core';
import {
  NOTIFICATION_CENTER_NAV_ITEM_PROVIDERS,
  NOTIFICATION_CENTER_ROUTE_PROVIDERS,
  NOTIFICATION_CENTER_SETTING_TAB_PROVIDERS,
} from './';

export interface NotificationCenterConfigOptions {}

/**
 * Registers the Notification Center's navigation-menu contribution, toolbar bell, and Settings tab
 * into the host. A consuming app calls this once in its `app.config.ts` providers — mirroring ABP's
 * own `provideXxxConfig()` packages (e.g. `provideIdentityConfig`). The bell opens its own SignalR
 * connection to the ABP-mapped hub, so no realtime wiring is needed here.
 */
export function provideNotificationCenterConfig(_options: NotificationCenterConfigOptions = {}) {
  return makeEnvironmentProviders([
    NOTIFICATION_CENTER_ROUTE_PROVIDERS,
    NOTIFICATION_CENTER_NAV_ITEM_PROVIDERS,
    NOTIFICATION_CENTER_SETTING_TAB_PROVIDERS,
  ]);
}
