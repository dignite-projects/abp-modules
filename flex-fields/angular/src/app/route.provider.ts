import { RoutesService, eLayoutType } from '@abp/ng.core';
import { provideAppInitializer, inject } from '@angular/core';
export const APP_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];
function configureRoutes() {
  const routes = inject(RoutesService);
  routes.add([
    {
      path: '/',
      name: '::Menu:Home',
      iconClass: 'fas fa-home',
      order: 1,
      layout: eLayoutType.application,
    },
    {
      path: '/products',
      name: '::Menu:Products',
      iconClass: 'fas fa-box',
      order: 2,
      layout: eLayoutType.application,
      requiredPolicy: 'Demo.Products',
    },
    {
      path: '/product-fields',
      name: '::Menu:ProductFields',
      iconClass: 'fas fa-list-alt',
      order: 3,
      layout: eLayoutType.application,
      requiredPolicy: 'Demo.ProductFields',
    },
  ]);
}
