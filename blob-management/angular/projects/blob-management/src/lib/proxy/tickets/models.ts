import type { BlobManagementTicketKind } from './blob-management-ticket-kind.enum';

export interface BlobManagementTicketDto {
  ticket?: string;
  kind?: BlobManagementTicketKind;
  expiresAt?: string;
  url?: string;
}

export interface CreateBlobManagementTicketInput {
  containerName: string;
  blobName: string;
  kind?: BlobManagementTicketKind;
  maxSizeInBytes?: number | null;
  overrideExisting?: boolean;
  lifetimeSeconds?: number | null;
}
