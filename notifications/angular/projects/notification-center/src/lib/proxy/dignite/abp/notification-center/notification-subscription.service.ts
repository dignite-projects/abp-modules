import type { NotificationSubscriptionDto, NotificationSubscriptionScopeDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { ListResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class NotificationSubscriptionService {
  private restService = inject(RestService);
  apiName = 'NotificationCenter';
  

  getSubscriptions = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ListResultDto<NotificationSubscriptionDto>>({
      method: 'GET',
      url: '/api/notification-center/subscriptions',
    },
    { apiName: this.apiName,...config });
  

  subscribe = (input: NotificationSubscriptionScopeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/notification-center/subscriptions',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  unsubscribe = (input: NotificationSubscriptionScopeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/notification-center/subscriptions',
      params: { notificationName: input.notificationName, entityTypeName: input.entityTypeName, entityId: input.entityId },
    },
    { apiName: this.apiName,...config });
}