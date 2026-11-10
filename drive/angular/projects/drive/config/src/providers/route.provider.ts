import { eLayoutType, RoutesService } from '@abp/ng.core';
import {
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { eDriveRouteNames } from '../enums/route-names';

export const DRIVE_ROUTE_PROVIDERS = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

export function configureRoutes() {
  const routesService = inject(RoutesService);
  routesService.add([
    {
      path: '/drive',
      name: eDriveRouteNames.Drive,
      iconClass: 'fas fa-book',
      layout: eLayoutType.application,
      order: 3,
    },
  ]);
}

const DRIVE_PROVIDERS: EnvironmentProviders[] = [...DRIVE_ROUTE_PROVIDERS];

export function provideDrive() {
  return makeEnvironmentProviders(DRIVE_PROVIDERS);
}
