/*
 * `@dignite/ng.blob-management/proxy`: the proxies of the APIs a browser calls - the containers, their directories,
 * the files the management page reads and the usage. Integration services (the BLOB API and the file API a business
 * system calls from its server) are not generated for Angular.
 *
 * Hand-written: `proxy/` is the output of
 * `abp generate-proxy -t ng -m blobManagement --source dev-app --target blob-management --entry-point proxy`, which
 * clears and rewrites it. The files are exported one by one, not through the generated barrels, as its README asks.
 */
export * from './proxy/models';
export * from './proxy/containers/blob-management-container.service';
export * from './proxy/containers/models';
export * from './proxy/directories/blob-management-directory.service';
export * from './proxy/directories/models';
export * from './proxy/files/blob-management-file.service';
export * from './proxy/files/models';
export * from './proxy/usage/blob-management-usage.service';
export * from './proxy/usage/models';
