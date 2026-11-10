import { inject, Injectable } from '@angular/core';
import { RestService } from '@abp/ng.core';

@Injectable({
  providedIn: 'root',
})
export class DriveService {
  apiName = 'Drive';

  private restService = inject(RestService);

  sample() {
    return this.restService.request<void, any>(
      { method: 'GET', url: '/api/drive/example' },
      { apiName: this.apiName }
    );
  }
}
