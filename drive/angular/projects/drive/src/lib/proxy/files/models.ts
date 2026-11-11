import type { IRemoteStreamContent } from '../volo/abp/content/models';
import type { ExtensibleCreationAuditedEntityDto } from '@abp/ng.core';

export interface CreateDriveFileInput {
  containerName: string;
  name?: string | null;
  file: IRemoteStreamContent;
}

export interface CreateDriveFileUploadTicketInput {
  containerName: string;
  fileName: string;
  lifetimeSeconds?: number | null;
}

export interface DriveFileDto extends ExtensibleCreationAuditedEntityDto<string> {
  containerName?: string;
  name?: string;
  mimeType?: string;
  size?: number;
  lastModificationTime?: string | null;
  url?: string | null;
}
