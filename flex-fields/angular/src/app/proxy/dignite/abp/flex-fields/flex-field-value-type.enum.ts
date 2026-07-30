import { mapEnumToOptions } from '@abp/ng.core';

export enum FlexFieldValueType {
  String = 0,
  Number = 1,
  DateTime = 2,
  Boolean = 3,
  Guid = 4,
}

export const flexFieldValueTypeOptions = mapEnumToOptions(FlexFieldValueType);
