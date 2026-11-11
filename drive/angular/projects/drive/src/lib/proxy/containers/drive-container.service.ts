import type { DriveContainerDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DriveContainerService {
  private restService = inject(RestService);
  apiName = 'Drive';
  

  
  get = (name: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DriveContainerDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/drive/containers/${name}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<DriveContainerDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/drive/containers',
    },
      { apiName: this.apiName,...config });
}
