import { mapEnumToOptions } from '@abp/ng.core';

export enum FlexFieldQueryOperator {
  Equals = 0,
  NotEquals = 1,
  Contains = 2,
  GreaterThan = 3,
  GreaterThanOrEqual = 4,
  LessThan = 5,
  LessThanOrEqual = 6,
  In = 7,
}

export const flexFieldQueryOperatorOptions = mapEnumToOptions(FlexFieldQueryOperator);
