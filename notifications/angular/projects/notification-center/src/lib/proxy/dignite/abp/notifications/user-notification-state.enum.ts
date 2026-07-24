import { mapEnumToOptions } from '@abp/ng.core';

export enum UserNotificationState {
  Unread = 0,
  Read = 1,
}

export const userNotificationStateOptions = mapEnumToOptions(UserNotificationState);
