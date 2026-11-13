/*
 * The proxies a browser application uses, exported by name. Hand-written, and kept outside `proxy/`, which
 * `abp generate-proxy` clears and rewrites; the generated `proxy/index.ts` exports namespaces only.
 *
 * Left out on purpose: the BLOB API (`proxy/blobs`, `BlobManagementBlobService` at `api/blob-management/blobs`). It
 * rejects a token that carries a user, so a browser cannot call it. The file API's proxy is here for its reads, which
 * the management page uses with the management permission; what it writes is refused to a user's token as well - a
 * business system stores, renames, moves and deletes files from its own server, as a client.
 */
export * from './proxy/containers';
export * from './proxy/directories';
export * from './proxy/files';
export * from './proxy/usage';
export type { BlobManagementAccessDto } from './proxy/models';
