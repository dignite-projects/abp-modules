/** The module-wide permissions the UI asks about (standard permissions, granted in ABP's permission dialog). */
export const eBlobManagementPolicyNames = {
  /** The management permission: the management page, and everything it reads. */
  Containers: 'BlobManagement.Containers',
  /** Manages the grants on containers. */
  ManagePermissions: 'BlobManagement.ManagePermissions',
} as const;

/**
 * The resource name Blob Management's resource permissions are granted under: the full name of the class on the
 * server.
 */
export const eBlobManagementResourceNames = {
  Container: 'Dignite.Abp.BlobManagement.BlobManagementContainer',
} as const;
