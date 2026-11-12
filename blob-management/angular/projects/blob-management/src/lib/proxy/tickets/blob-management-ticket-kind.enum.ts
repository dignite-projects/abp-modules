import { mapEnumToOptions } from '@abp/ng.core';

export enum BlobManagementTicketKind {
  Upload = 0,
  Download = 1,
  FileUpload = 2,
}

export const blobManagementTicketKindOptions = mapEnumToOptions(BlobManagementTicketKind);
