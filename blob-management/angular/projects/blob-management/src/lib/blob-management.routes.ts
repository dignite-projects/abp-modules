import {
  authGuard,
  permissionGuard,
  ReplaceableComponents,
  ReplaceableRouteContainerComponent,
  RouterOutletComponent,
} from '@abp/ng.core';
import { Routes } from '@angular/router';
import { eBlobManagementPolicyNames } from './blob-management-permissions';
import { BlobManagementComponent } from './components/blob-management.component';
import { eBlobManagementComponents } from './enums/components';

/**
 * The management page, for holders of `BlobManagement.Containers`:
 * `{ path: 'blob-management', loadChildren: () => import('@dignite/ng.blob-management').then(m => m.createRoutes()) }`.
 * An application replaces the page by registering its own component under `eBlobManagementComponents.BlobManagement`.
 */
export const createRoutes = (): Routes => [
  {
    path: '',
    component: RouterOutletComponent,
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: eBlobManagementPolicyNames.Containers },
    children: [
      {
        path: '',
        component: ReplaceableRouteContainerComponent,
        data: {
          replaceableComponent: {
            key: eBlobManagementComponents.BlobManagement,
            defaultComponent: BlobManagementComponent,
          } as ReplaceableComponents.RouteData<BlobManagementComponent>,
        },
        title: 'BlobManagement::Menu:BlobManagement',
      },
    ],
  },
];
