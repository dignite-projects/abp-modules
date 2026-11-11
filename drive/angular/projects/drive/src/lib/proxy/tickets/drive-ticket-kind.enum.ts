import { mapEnumToOptions } from '@abp/ng.core';

export enum DriveTicketKind {
  Upload = 0,
  Download = 1,
  FileUpload = 2,
}

export const driveTicketKindOptions = mapEnumToOptions(DriveTicketKind);
