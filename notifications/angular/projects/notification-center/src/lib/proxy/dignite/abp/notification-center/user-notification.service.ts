import type { GetUserNotificationListInput, UserNotificationDto } from './models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class UserNotificationService {
  private restService = inject(RestService);
  apiName = 'NotificationCenter';
  

  delete = (notificationId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/notification-center/notifications/${notificationId}`,
    },
    { apiName: this.apiName,...config });
  

  deleteAllRead = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/notification-center/notifications/read',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetUserNotificationListInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<UserNotificationDto>>({
      method: 'GET',
      url: '/api/notification-center/notifications',
      params: { state: input.state, startDate: input.startDate, endDate: input.endDate, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getUnreadCount = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'GET',
      url: '/api/notification-center/notifications/unread-count',
    },
    { apiName: this.apiName,...config });
  

  markAllAsRead = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/notification-center/notifications/mark-all-as-read',
    },
    { apiName: this.apiName,...config });
  

  markAsRead = (notificationId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/notification-center/notifications/${notificationId}/mark-as-read`,
    },
    { apiName: this.apiName,...config });
}