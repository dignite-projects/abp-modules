/*
 * The proxies a browser application uses, exported by name. Hand-written, and kept outside `proxy/`, which
 * `abp generate-proxy` clears and rewrites; the generated `proxy/index.ts` exports namespaces only.
 *
 * Left out on purpose: the integration entry (`proxy/blobs`, `DriveBlobService` at `api/drive/blobs`) and the system
 * tickets (`DriveTicketService`, `CreateDriveTicketInput`). They reject a token that carries a user, so a browser
 * cannot call them.
 */
export * from './proxy/containers';
export * from './proxy/files';
export * from './proxy/tickets/drive-ticket-kind.enum';
export type { DriveTicketDto } from './proxy/tickets/models';
