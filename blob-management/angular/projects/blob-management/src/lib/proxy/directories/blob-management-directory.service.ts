import type { BlobManagementDirectoryDto, CreateBlobManagementDirectoryInput, GetBlobManagementDirectoriesInput, MoveBlobManagementDirectoryInput, RestrictBlobManagementDirectoryInput, UpdateBlobManagementDirectoryInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BlobManagementDirectoryService {
  private restService = inject(RestService);
  apiName = 'BlobManagement';
  

  
  create = (input: CreateBlobManagementDirectoryInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementDirectoryDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/directories',
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/blob-management/directories/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementDirectoryDto>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/directories/${id}`,
    },
      { apiName: this.apiName,...config });
  

  
  getList = (input: GetBlobManagementDirectoriesInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<BlobManagementDirectoryDto>>({
      method: 'GET',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/directories',
      params: { containerName: input.containerName, parentId: input.parentId },
    },
      { apiName: this.apiName,...config });
  

  
  move = (id: string, input: MoveBlobManagementDirectoryInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementDirectoryDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/directories/${id}/move`,
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  restrict = (id: string, input: RestrictBlobManagementDirectoryInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementDirectoryDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/directories/${id}/restriction`,
      body: input,
    },
      { apiName: this.apiName,...config });
  

  
  update = (id: string, input: UpdateBlobManagementDirectoryInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementDirectoryDto>({
      method: 'PUT',
      headers: { Accept: 'application/json' },
      url: `/api/blob-management/directories/${id}`,
      body: input,
    },
      { apiName: this.apiName,...config });
}
