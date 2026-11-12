import type { BlobManagementTicketDto, CreateBlobManagementTicketInput } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class BlobManagementTicketService {
  private restService = inject(RestService);
  apiName = 'BlobManagement';
  

  
  create = (input: CreateBlobManagementTicketInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BlobManagementTicketDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/blob-management/tickets',
      body: input,
    },
      { apiName: this.apiName,...config });
}
