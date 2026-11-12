import type { ExtensibleObject } from '@abp/ng.core';

export interface BlobManagementContainerDto extends ExtensibleObject {
  name?: string;
  publicRead?: boolean;
  maxFileSize?: number;
  allowedFileExtensions?: string[];
  imageMaxWidth?: number | null;
  imageMaxHeight?: number | null;
}
