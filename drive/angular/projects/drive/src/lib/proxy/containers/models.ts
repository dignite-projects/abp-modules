import type { ExtensibleObject } from '@abp/ng.core';

export interface DriveContainerDto extends ExtensibleObject {
  name?: string;
  publicRead?: boolean;
  maxFileSize?: number;
  allowedFileExtensions?: string[];
  imageMaxWidth?: number | null;
  imageMaxHeight?: number | null;
}
