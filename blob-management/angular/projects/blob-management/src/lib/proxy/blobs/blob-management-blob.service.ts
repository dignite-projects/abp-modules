import type { BlobManagementBlobDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BlobManagementBlobService {
  private restService = inject(RestService);
  apiName = 'BlobManagement';
  

  
  delete = (containerName: string, blobName: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'DELETE',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/blobs/${containerName}`,
      params: { blobName },
    },
      { apiName: this.apiName,...config });
  

  
  exists = (containerName: string, blobName: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/blobs/${containerName}/exists`,
      params: { blobName },
    },
      { apiName: this.apiName,...config });
  

  
  getOrNull = (containerName: string, blobName: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      headers: { Accept: 'application/octet-stream' },
      url: `/api/blob-management/blobs/${containerName}`,
      params: { blobName },
    },
      { apiName: this.apiName,...config });
  

  
  save = (containerName: string, blobName: string, content: FormData, overrideExisting?: boolean, creatorId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementBlobDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/blobs/${containerName}`,
      params: { blobName, overrideExisting, creatorId },
      body: content,
    },
      { apiName: this.apiName,...config });
}
