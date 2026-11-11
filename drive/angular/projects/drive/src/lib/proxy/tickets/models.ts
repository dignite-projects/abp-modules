import type { DriveTicketKind } from './drive-ticket-kind.enum';

export interface CreateDriveTicketInput {
  containerName: string;
  blobName: string;
  kind?: DriveTicketKind;
  maxSizeInBytes?: number | null;
  overrideExisting?: boolean;
  lifetimeSeconds?: number | null;
}

export interface DriveTicketDto {
  ticket?: string;
  kind?: DriveTicketKind;
  expiresAt?: string;
  url?: string;
}
