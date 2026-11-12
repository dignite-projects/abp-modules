import type { CreateBlobManagementFileInput, CreateBlobManagementFileUploadTicketInput, BlobManagementFileDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { BlobManagementTicketDto } from '../tickets/models';

@Injectable({
  providedIn: 'root',
})
export class BlobManagementFileService {
  private restService = inject(RestService);
  apiName = 'BlobManagement';
  

  
  create = (input: FormData, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementFileDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/files',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  createUploadTicket = (input: CreateBlobManagementFileUploadTicketInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementTicketDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/files/upload-tickets',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/blob-management/files/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
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
}
