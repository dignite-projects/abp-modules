import {
  authGuard,
  permissionGuard,
  ReplaceableComponents,
  ReplaceableRouteContainerComponent,
  RouterOutletComponent,
} from '@abp/ng.core';
import { Routes } from '@angular/router';
import { eBlobManagementPolicyNames } from '@dignite/ng.blob-management/config';
import { BlobManagementFilesComponent } from './components/blob-management-files.component';
import { BlobManagementUsageComponent } from './components/blob-usage/blob-usage.component';
import { eBlobManagementComponents } from './enums/components';

/**
 * The management pages, for holders of `BlobManagement.Containers`:
 * `{ path: 'blob-management', loadChildren: () => import('@dignite/ng.blob-management').then(m => m.createRoutes()) }`.
 * `/blob-management/usage` is what the tenant stores and `/blob-management/files` its containers' directories and files;
 * `/blob-management` opens the first. An application replaces a page by registering its own component under
 * `eBlobManagementComponents.Usage` or `eBlobManagementComponents.Files`.
 */
export const createRoutes = (): Routes => [
  {
    path: '',
    component: RouterOutletComponent,
    canActivate: [authGuard, permissionGuard],
    data: { requiredPolicy: eBlobManagementPolicyNames.Containers },
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'usage' },
      {
        path: 'usage',
        component: ReplaceableRouteContainerComponent,
        data: {
          replaceableComponent: {
            key: eBlobManagementComponents.Usage,
            defaultComponent: BlobManagementUsageComponent,
          } as ReplaceableComponents.RouteData<BlobManagementUsageComponent>,
        },
        title: 'BlobManagement::Usage',
      },
      {
        path: 'files',
        component: ReplaceableRouteContainerComponent,
        data: {
          replaceableComponent: {
            key: eBlobManagementComponents.Files,
            defaultComponent: BlobManagementFilesComponent,
          } as ReplaceableComponents.RouteData<BlobManagementFilesComponent>,
        },
        title: 'BlobManagement::Files',
      },
    ],
  },
];
