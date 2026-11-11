import type { CreateDriveFileInput, CreateDriveFileUploadTicketInput, DriveFileDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { DriveTicketDto } from '../tickets/models';

@Injectable({
  providedIn: 'root',
})
export class DriveFileService {
  private restService = inject(RestService);
  apiName = 'Drive';
  

  
  create = (input: FormData, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DriveFileDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/drive/files',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  createUploadTicket = (input: CreateDriveFileUploadTicketInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DriveTicketDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/drive/files/upload-tickets',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/drive/files/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DriveFileDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/drive/files/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getContent = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      headers: { Accept: 'application/octet-stream' },
      url: `/api/drive/files/${id}/content`,
    },
      { apiName: this.apiName,...config });
}
