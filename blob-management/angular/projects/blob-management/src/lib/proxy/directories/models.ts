import type { CreationAuditedEntityDto } from '@abp/ng.core';
import type { BlobManagementAccessDto } from '../models';

export interface BlobManagementDirectoryDto extends CreationAuditedEntityDto<string> {
  containerName?: string;
  name?: string;
  parentId?: string | null;
  order?: number;
  hasChildren?: boolean;
  isRestricted?: boolean;
  canUpdate?: boolean;
  canDelete?: boolean;
  access?: BlobManagementAccessDto;
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

export interface RestrictBlobManagementDirectoryInput {
  isRestricted?: boolean;
}

export interface UpdateBlobManagementDirectoryInput {
  name: string;
}
