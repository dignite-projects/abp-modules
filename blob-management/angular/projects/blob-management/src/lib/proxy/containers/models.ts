import type { ExtensibleObject } from '@abp/ng.core';
import type { BlobManagementAccessDto } from '../models';

export interface BlobManagementContainerDto extends ExtensibleObject {
  name?: string;
  allowDirectories?: boolean;
  maxFileSize?: number;
  allowedFileExtensions?: string[];
  imageMaxWidth?: number | null;
  imageMaxHeight?: number | null;
  access?: BlobManagementAccessDto;
}
