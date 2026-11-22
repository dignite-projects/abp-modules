import type { BlobManagementFileDto, GetBlobManagementFilesInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BlobManagementFileService {
  private restService = inject(RestService);
  apiName = 'BlobManagement';
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementFileDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/files/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getContent = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      headers: { Accept: 'application/octet-stream' },
      url: `/api/blob-management/files/${id}/content`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetBlobManagementFilesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BlobManagementFileDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/files',
      params: { containerName: input.containerName, directoryId: input.directoryId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
}
