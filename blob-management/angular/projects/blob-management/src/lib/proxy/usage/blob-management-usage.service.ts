import type { BlobManagementClientUsageDto, BlobManagementUsageDto, BlobManagementUserUsageDto, GetBlobManagementClientUsagesInput, GetBlobManagementUserUsagesInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BlobManagementUsageService {
  private restService = inject(RestService);
  apiName = 'BlobManagement';
  

  
  get = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementUsageDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/usage',
    },
      { apiName: this.apiName,...config });
  

  
  getClients = (input: GetBlobManagementClientUsagesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<BlobManagementClientUsageDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/usage/clients',
      params: { containerName: input.containerName },
    },
      { apiName: this.apiName,...config });
  

  
  getUsers = (input: GetBlobManagementUserUsagesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BlobManagementUserUsageDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/usage/users',
      params: { containerName: input.containerName, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
}
