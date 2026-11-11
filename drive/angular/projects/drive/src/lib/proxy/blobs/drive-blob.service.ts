import type { DriveBlobDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DriveBlobService {
  private restService = inject(RestService);
  apiName = 'Drive';
  

  
  delete = (containerName: string, blobName: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'DELETE',
      headers: { Accept: 'application/json' },
      url: `/api/drive/blobs/${containerName}`,
      params: { blobName },
    },
      { apiName: this.apiName,...config });
  

  
  exists = (containerName: string, blobName: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/drive/blobs/${containerName}/exists`,
      params: { blobName },
    },
      { apiName: this.apiName,...config });
  

  
  getOrNull = (containerName: string, blobName: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      headers: { Accept: 'application/octet-stream' },
      url: `/api/drive/blobs/${containerName}`,
      params: { blobName },
    },
      { apiName: this.apiName,...config });
  

  
  save = (containerName: string, blobName: string, content: FormData, overrideExisting?: boolean, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DriveBlobDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/drive/blobs/${containerName}`,
      params: { blobName, overrideExisting },
      body: content,
    },
      { apiName: this.apiName,...config });
}
