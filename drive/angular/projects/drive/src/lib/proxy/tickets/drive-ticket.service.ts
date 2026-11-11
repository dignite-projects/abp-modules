import type { CreateDriveTicketInput, DriveTicketDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class DriveTicketService {
  private restService = inject(RestService);
  apiName = 'Drive';
  

  
  create = (input: CreateDriveTicketInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DriveTicketDto>({
      method: 'POST',
      headers: { Accept: 'application/json' },
      url: '/api/drive/tickets',
      body: input,
    },
      { apiName: this.apiName,...config });
}
