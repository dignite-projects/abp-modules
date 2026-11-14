import type { ExtensibleCreationAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { IRemoteStreamContent } from '../volo/abp/content/models';

export interface BlobManagementFileDto extends ExtensibleCreationAuditedEntityDto<string> {
  containerName?: string;
  directoryId?: string | null;
  name?: string;
  isAddressedByName?: boolean;
  mimeType?: string;
  size?: number;
  clientId?: string | null;
  lastModificationTime?: string | null;
  isPublic?: boolean;
  isPending?: boolean;
  url?: string | null;
}

export interface BlobManagementUploadTicketDto {
  ticket?: string;
  uploadUrl?: string;
  fileId?: string;
  expirationTime?: string;
}

export interface CreateBlobManagementFileInput {
  containerName: string;
  directoryId?: string | null;
  name?: string | null;
  creatorId?: string | null;
  isPublic?: boolean;
  file: IRemoteStreamContent;
}

export interface CreateBlobManagementUploadTicketInput {
  containerName: string;
  directoryId?: string | null;
  uploaderId?: string | null;
  maxSize?: number | null;
  allowedContentTypes?: string[];
  lifetimeInSeconds?: number | null;
}

export interface GetBlobManagementFilesInput extends PagedAndSortedResultRequestDto {
  containerName: string;
  directoryId?: string | null;
  filter?: string | null;
}

export interface MoveBlobManagementFileInput {
  directoryId?: string | null;
}

export interface SetBlobManagementFilePublicInput {
  isPublic?: boolean;
}

export interface UpdateBlobManagementFileInput {
  name: string;
}
