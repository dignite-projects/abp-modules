import type { IRemoteStreamContent } from '../volo/abp/content/models';
import type { ExtensibleCreationAuditedEntityDto } from '@abp/ng.core';

export interface CreateBlobManagementFileInput {
  containerName: string;
  name?: string | null;
  file: IRemoteStreamContent;
}

export interface CreateBlobManagementFileUploadTicketInput {
  containerName: string;
  fileName: string;
  lifetimeSeconds?: number | null;
}

export interface BlobManagementFileDto extends ExtensibleCreationAuditedEntityDto<string> {
  containerName?: string;
  name?: string;
  mimeType?: string;
  size?: number;
  lastModificationTime?: string | null;
  url?: string | null;
}
