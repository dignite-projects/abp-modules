import { eLayoutType, RoutesService } from '@abp/ng.core';
import { EnvironmentProviders, inject, provideAppInitializer } from '@angular/core';
import { eFileRouteNames } from '../enums';

export const FILE_ROUTE_PROVIDERS: EnvironmentProviders[] = [
  provideAppInitializer(() => {
    configureRoutes(inject(RoutesService));
  }),
];

export function configureRoutes(routesService: RoutesService): void {
  routesService.add([
    {
      path: '/file/file-dome',
      name: eFileRouteNames.FileUploadDemo,
      iconClass: 'fas fa fa-file-archive-o',
      layout: eLayoutType.application,
      order: 9,
    },
  ]);
}
