import { eLayoutType, RoutesService } from '@abp/ng.core';
import {
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { eBlobManagementRouteNames } from '../enums/route-names';

export const BLOB_MANAGEMENT_ROUTE_PROVIDERS = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];

export function configureRoutes() {
  const routesService = inject(RoutesService);
  routesService.add([
    {
      path: '/blob-management',
      name: eBlobManagementRouteNames.BlobManagement,
      parentName: 'AbpUiNavigation::Menu:Administration',
      iconClass: 'fas fa-folder-open',
      layout: eLayoutType.application,
      requiredPolicy: 'BlobManagement.Containers',
      order: 100,
    },
  ]);
}

const BLOB_MANAGEMENT_PROVIDERS: EnvironmentProviders[] = [...BLOB_MANAGEMENT_ROUTE_PROVIDERS];

/**
 * The management page's menu item, under Administration, for holders of `BlobManagement.Containers`. Route the page
 * itself with `createRoutes()` from `@dignite/ng.blob-management`.
 */
export function provideBlobManagementConfig() {
  return makeEnvironmentProviders(BLOB_MANAGEMENT_PROVIDERS);
}
