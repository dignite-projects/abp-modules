import { eLayoutType, RoutesService } from '@abp/ng.core';
import { eThemeSharedRouteNames } from '@abp/ng.theme.shared';
import {
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import { eBlobManagementPolicyNames } from '../enums/policy-names';
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
      path: undefined,
      name: eBlobManagementRouteNames.BlobManagement,
      parentName: eThemeSharedRouteNames.Administration,
      iconClass: 'fas fa-folder-open',
      layout: eLayoutType.application,
      requiredPolicy: eBlobManagementPolicyNames.Containers,
      order: 100,
    },
    {
      path: '/blob-management/usage',
      name: eBlobManagementRouteNames.Usage,
      parentName: eBlobManagementRouteNames.BlobManagement,
      requiredPolicy: eBlobManagementPolicyNames.Containers,
      order: 1,
    },
    {
      path: '/blob-management/files',
      name: eBlobManagementRouteNames.Files,
      parentName: eBlobManagementRouteNames.BlobManagement,
      requiredPolicy: eBlobManagementPolicyNames.Containers,
      order: 2,
    },
  ]);
}

const BLOB_MANAGEMENT_PROVIDERS: EnvironmentProviders[] = [...BLOB_MANAGEMENT_ROUTE_PROVIDERS];

/**
 * The management pages' menu: Blob Management under Administration, with Usage and Files below it, for holders of
 * `BlobManagement.Containers`. Route the pages themselves with `createRoutes()` from `@dignite/ng.blob-management`.
 */
export function provideBlobManagementConfig() {
  return makeEnvironmentProviders(BLOB_MANAGEMENT_PROVIDERS);
}
