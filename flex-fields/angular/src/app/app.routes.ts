import { authGuard, permissionGuard } from '@abp/ng.core';
import { Routes } from '@angular/router';
export const APP_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home/home.component').then(c => c.HomeComponent),
  },
  {
    path: 'account',
    loadChildren: () => import('@abp/ng.account').then(c => c.createRoutes()),
  },
  {
    path: 'identity',
    loadChildren: () => import('@abp/ng.identity').then(c => c.createRoutes()),
  },
  {
    path: 'setting-management',
    loadChildren: () => import('@abp/ng.setting-management').then(c => c.createRoutes()),
  },
  {
    path: 'products',
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: 'Demo.Products' },
    loadComponent: () => import('./products/products.component').then(c => c.ProductsComponent),
  },
  {
    path: 'product-fields',
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: 'Demo.ProductFields' },
    loadComponent: () =>
      import('./product-fields/product-fields.component').then(c => c.ProductFieldsComponent),
  },
];
