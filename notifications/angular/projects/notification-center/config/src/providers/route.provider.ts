import { eLayoutType, RoutesService } from '@abp/ng.core';
import { inject, provideAppInitializer } from '@angular/core';
import { eNotificationCenterRouteNames } from '../enums/route-names';

export const NOTIFICATION_CENTER_ROUTE_PROVIDERS = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

export function configureRoutes() {
  const routes = inject(RoutesService);
  routes.add([
    {
      path: '/notifications',
      name: eNotificationCenterRouteNames.Notifications,
      iconClass: 'fas fa-bell',
      order: 2,
      layout: eLayoutType.application,
    },
  ]);
}
