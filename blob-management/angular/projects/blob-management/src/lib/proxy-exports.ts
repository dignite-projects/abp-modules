/*
 * The proxies a browser application uses, exported by name. Hand-written, and kept outside `proxy/`, which
 * `abp generate-proxy` clears and rewrites; the generated `proxy/index.ts` exports namespaces only.
 *
 * Left out on purpose: the BLOB API (`proxy/blobs`, `BlobManagementBlobService` at `api/blob-management/blobs`) and the
 * clients' tickets (`BlobManagementTicketService`, `CreateBlobManagementTicketInput`). They reject a token that carries
 * a user, so a browser cannot call them.
 */
export * from './proxy/containers';
export * from './proxy/directories';
export * from './proxy/files';
export type { BlobManagementAccessDto } from './proxy/models';
export * from './proxy/tickets/blob-management-ticket-kind.enum';
export type { BlobManagementTicketDto } from './proxy/tickets/models';
