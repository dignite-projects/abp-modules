/** The module-wide permissions the UI asks about (standard permissions, granted in ABP's permission dialog). */
export const eBlobManagementPolicyNames = {
  /** The management permission: the management pages, and everything they read. */
  Containers: 'BlobManagement.Containers',
  /** Usage by tenant: defined for the host side only, so it is never granted in a tenant. */
  TenantUsages: 'BlobManagement.Containers.TenantUsages',
  /** Manages the grants on containers. */
  ManagePermissions: 'BlobManagement.ManagePermissions',
} as const;
