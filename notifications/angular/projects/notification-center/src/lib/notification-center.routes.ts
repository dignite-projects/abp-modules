import { RouterOutletComponent, ReplaceableRouteContainerComponent, authGuard } from '@abp/ng.core';
import { Routes } from '@angular/router';
import { eNotificationCenterComponents } from './enums/components';
import { NotificationInboxComponent } from './notification-inbox/notification-inbox.component';

/**
 * Lazy routes for the Notification Center pages, following ABP's `createRoutes()` convention (as in
 * `@abp/ng.identity`). Mount them at the path the config package registers (`/notifications`):
 *
 * ```ts
 * { path: 'notifications', loadChildren: () => import('@dignite/ng.notification-center').then(c => c.createRoutes()) }
 * ```
 *
 * The inbox is replaceable through `eNotificationCenterComponents.Notifications`.
 */
export const createRoutes = (): Routes => [
  {
    path: '',
    component: RouterOutletComponent,
    canActivate: [authGuard],
    children: [
      {
        path: '',
        component: ReplaceableRouteContainerComponent,
        data: {
          replaceableComponent: {
            key: eNotificationCenterComponents.Notifications,
            defaultComponent: NotificationInboxComponent,
          },
        },
        title: 'NotificationCenter::Notifications',
      },
    ],
  },
];
