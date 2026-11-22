import type { ExtensibleCreationAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

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

export interface GetBlobManagementFilesInput extends PagedAndSortedResultRequestDto {
  containerName: string;
  directoryId?: string | null;
  filter?: string | null;
}
