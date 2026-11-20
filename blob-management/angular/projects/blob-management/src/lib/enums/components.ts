/** The keys under which an application replaces Blob Management's components (`ReplaceableComponentsService`). */
export const enum eBlobManagementComponents {
  /** The usage page: what the tenant stores, by container, user, client - and, in the host, by tenant. */
  Usage = 'BlobManagement.BlobManagementUsageComponent',
  /** The files page: one container at a time, its directory tree and its files. */
  Files = 'BlobManagement.BlobManagementFilesComponent',
}
