/** The module-wide permissions the UI asks about (standard permissions, granted in ABP's permission dialog). */
export const eBlobManagementPolicyNames = {
  /** The management permission: the back office, every container. */
  Containers: 'BlobManagement.Containers',
  /** Manages the grants on containers and directories, and which directories are restricted. */
  ManagePermissions: 'BlobManagement.ManagePermissions',
} as const;

/**
 * The resource names Blob Management's resource permissions are granted under: the full names of the classes on the
 * server.
 */
export const eBlobManagementResourceNames = {
  Container: 'Dignite.Abp.BlobManagement.BlobManagementContainer',
  Directory: 'Dignite.Abp.BlobManagement.DirectoryDescriptor',
} as const;

/** What ABP's resource-permission dialog is opened for. */
export interface BlobManagementPermissionTarget {
  resourceName: string;
  resourceKey: string;
  displayName: string;
}
