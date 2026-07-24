import type { IRemoteStreamContent } from '../../../volo/abp/content/models';
import type { CreationAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface CreateFileInput {
  containerName: string;
  cellName?: string;
  directoryId?: string;
  entityId?: string;
  file: IRemoteStreamContent;
}

export interface FileCellDto {
  name?: string;
  displayName?: string;
}

export interface FileContainerConfigurationDto {
  maxBlobSize: number;
  allowedFileTypeNames: string[];
  fileCells: FileCellDto[];
  createDirectoryPermissionName?: string;
  createFilePermissionName?: string;
  updateFilePermissionName?: string;
  deleteFilePermissionName?: string;
  getFilePermissionName?: string;
}

export interface FileDescriptorDto extends FileDescriptorListDto {
  url?: string;
  tenantId?: string;
}

export interface FileDescriptorListDto extends CreationAuditedEntityDto<string> {
  entityId?: string;
  containerName?: string;
  blobName?: string;
  cellName?: string;
  directoryId?: string;
  size: number;
  name?: string;
  mimeType?: string;
}

export interface GetFilesInput extends PagedAndSortedResultRequestDto {
  containerName: string;
  directoryId?: string;
  creatorId?: string;
  filter?: string;
  entityId?: string;
}

export interface ImageResizeInput {
  width?: number;
  height?: number;
}

export interface UpdateFileInput {
  /** Set to null to clear the cell assignment. */
  cellName?: string | null;
  /** Set to null to move the file to the container root. */
  directoryId?: string | null;
  name?: string;
}
