import type { CreateProductFieldDto, FieldTypeDto, GetProductFieldListDto, ProductFieldDto, UpdateProductFieldDto } from './dtos/models';
import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class ProductFieldService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateProductFieldDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductFieldDto>({
      method: 'POST',
      url: '/api/app/product-field',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/product-field/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductFieldDto>({
      method: 'GET',
      url: `/api/app/product-field/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getFieldTypes = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, FieldTypeDto[]>({
      method: 'GET',
      url: '/api/app/product-field/field-types',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: GetProductFieldListDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<ProductFieldDto>>({
      method: 'GET',
      url: '/api/app/product-field',
      params: { skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateProductFieldDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ProductFieldDto>({
      method: 'PUT',
      url: `/api/app/product-field/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}