import type { BlobManagementFileDto, BlobManagementReadLinkDto, BlobManagementUploadTicketDto, CreateBlobManagementFileInput, CreateBlobManagementReadLinkInput, CreateBlobManagementReadLinksInput, CreateBlobManagementUploadTicketInput, GetBlobManagementFilesInput, MoveBlobManagementFileInput, SetBlobManagementFilePublicInput, UpdateBlobManagementFileInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BlobManagementFileService {
  private restService = inject(RestService);
  apiName = 'BlobManagement';
  

  
  confirm = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementFileDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/files/${id}/confirm`,
    },
      { apiName: this.apiName,...config });
  

  
  create = (input: FormData, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementFileDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/files',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  createReadLink = (id: string, input: CreateBlobManagementReadLinkInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementReadLinkDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/files/${id}/read-link`,
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  createReadLinks = (input: CreateBlobManagementReadLinksInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<BlobManagementReadLinkDto>>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/files/read-links',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  createUploadTicket = (input: CreateBlobManagementUploadTicketInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementUploadTicketDto>({
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
  

  
  getList = (input: GetBlobManagementFilesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BlobManagementFileDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/files',
      params: { containerName: input.containerName, directoryId: input.directoryId, filter: input.filter, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
      { apiName: this.apiName,...config });
  

  
  move = (id: string, input: MoveBlobManagementFileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementFileDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/files/${id}/move`,
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  setPublic = (id: string, input: SetBlobManagementFilePublicInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementFileDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/files/${id}/public`,
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: UpdateBlobManagementFileInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementFileDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/files/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
