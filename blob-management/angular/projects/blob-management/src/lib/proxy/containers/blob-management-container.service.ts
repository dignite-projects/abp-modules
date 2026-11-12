import type { BlobManagementContainerDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BlobManagementContainerService {
  private restService = inject(RestService);
  apiName = 'BlobManagement';
  

  
  get = (name: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementContainerDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/containers/${name}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<BlobManagementContainerDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/containers',
    },
      { apiName: this.apiName,...config });
}
