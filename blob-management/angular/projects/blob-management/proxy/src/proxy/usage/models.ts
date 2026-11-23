import type { PagedResultRequestDto } from '@abp/ng.core';

export interface BlobManagementClientUsageDto {
  clientId?: string | null;
  storedBytes?: number;
  fileCount?: number;
}

export interface BlobManagementContainerUsageDto {
  containerName?: string;
  storedBytes?: number;
  fileCount?: number;
}

export interface BlobManagementTenantUsageDto {
  tenantId?: string | null;
  tenantName?: string | null;
  storedBytes?: number;
  fileCount?: number;
  storageSize?: number | null;
}

export interface BlobManagementUsageDto {
  storedBytes?: number;
  storageSize?: number | null;
  fileCount?: number;
  deletedBytes?: number;
  deletedFileCount?: number;
  containers?: BlobManagementContainerUsageDto[];
}

export interface BlobManagementUserUsageDto {
  userId?: string | null;
  userName?: string | null;
  storedBytes?: number;
  fileCount?: number;
}

export interface GetBlobManagementClientUsagesInput {
  containerName?: string | null;
}

export interface GetBlobManagementTenantUsagesInput extends PagedResultRequestDto {
}

export interface GetBlobManagementUserUsagesInput extends PagedResultRequestDto {
  containerName?: string | null;
}
