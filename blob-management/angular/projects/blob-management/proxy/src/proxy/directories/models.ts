import type { CreationAuditedEntityDto } from '@abp/ng.core';

export interface BlobManagementDirectoryDto extends CreationAuditedEntityDto<string> {
  containerName?: string;
  name?: string;
  parentId?: string | null;
  order?: number;
  hasChildren?: boolean;
}

export interface CreateBlobManagementDirectoryInput {
  containerName: string;
  parentId?: string | null;
  name: string;
}

export interface GetBlobManagementDirectoriesInput {
  containerName: string;
  parentId?: string | null;
}

export interface MoveBlobManagementDirectoryInput {
  parentId?: string | null;
  order?: number;
}

export interface UpdateBlobManagementDirectoryInput {
  name: string;
}
