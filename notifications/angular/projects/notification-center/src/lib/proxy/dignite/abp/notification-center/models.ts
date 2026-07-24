import type { EntityDto, PagedResultRequestDto } from '@abp/ng.core';
import type { UserNotificationState } from '../notifications/user-notification-state.enum';
import type { NotificationData } from '../notifications/models';
import type { NotificationSeverity } from '../notifications/notification-severity.enum';

export interface GetUserNotificationListInput extends PagedResultRequestDto {
  state?: UserNotificationState | null;
  startDate?: string | null;
  endDate?: string | null;
}

export interface NotificationSubscriptionDto {
  notificationName?: string;
  entityTypeName?: string | null;
  entityId?: string | null;
  displayName?: string | null;
  description?: string | null;
  isSubscribed?: boolean;
}

export interface NotificationSubscriptionScopeDto {
  notificationName: string;
  entityTypeName?: string | null;
  entityId?: string | null;
}

export interface UserNotificationDto extends EntityDto<string> {
  userId?: string;
  notificationId?: string;
  notificationName?: string;
  notificationDisplayName?: string | null;
  data?: NotificationData | null;
  entityTypeName?: string | null;
  entityId?: string | null;
  severity?: NotificationSeverity;
  creationTime?: string;
  state?: UserNotificationState;
}
