import { RouterOutletComponent } from '@abp/ng.core';
import { Routes } from '@angular/router';

export const DRIVE_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    component: RouterOutletComponent,
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./components/drive.component').then(c => c.DriveComponent),
      },
    ],
  },
];
