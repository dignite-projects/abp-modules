import type { FlexFieldQueryOperator } from '../../dignite/abp/flex-fields/flex-field-query-operator.enum';
import type { FlexFieldValueType } from '../../dignite/abp/flex-fields/flex-field-value-type.enum';
import type { PagedResultRequestDto } from '@abp/ng.core';

export interface CreateProductFieldDto {
  name: string;
  displayName: string;
  description?: string | null;
  fieldTypeName: string;
  configuration?: Record<string, object>;
  required?: boolean;
  searchable?: boolean;
}

export interface CreateUpdateProductDto {
  name: string;
  flexFields?: Record<string, object>;
}

export interface FieldTypeDto {
  name?: string;
  indexable?: boolean;
}

export interface FlexFieldDataDto {
  id?: string;
  name?: string;
  displayName?: string;
  description?: string | null;
  fieldTypeName?: string;
  configuration?: Record<string, object>;
}

export interface FlexFieldQueryConditionDto {
  fieldId?: string;
  fieldName?: string;
  operator?: FlexFieldQueryOperator;
  value?: string;
  valueType?: FlexFieldValueType;
}

export interface FlexFieldValueDto {
  field?: FlexFieldDataDto;
  required?: boolean;
  searchable?: boolean;
  value?: object | null;
}

export interface GetProductFieldListDto extends PagedResultRequestDto {
}

export interface GetProductListDto extends PagedResultRequestDto {
  flexFieldConditions?: FlexFieldQueryConditionDto[];
}

export interface ProductDto {
  id?: string;
  name?: string;
  flexFieldValues?: FlexFieldValueDto[];
}

export interface ProductFieldDto {
  id?: string;
  name?: string;
  displayName?: string;
  description?: string | null;
  fieldTypeName?: string;
  configuration?: Record<string, object>;
  required?: boolean;
  searchable?: boolean;
}

export interface UpdateProductFieldDto {
  name: string;
  displayName: string;
  description?: string | null;
  configuration?: Record<string, object>;
  required?: boolean;
  searchable?: boolean;
}
