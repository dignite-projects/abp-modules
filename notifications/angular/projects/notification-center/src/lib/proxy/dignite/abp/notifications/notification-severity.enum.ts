import { mapEnumToOptions } from '@abp/ng.core';

export enum NotificationSeverity {
  Info = 0,
  Success = 10,
  Warn = 20,
  Error = 30,
}

export const notificationSeverityOptions = mapEnumToOptions(NotificationSeverity);
