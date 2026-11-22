# @dignite/ng.blob-management

The Angular UI of [Dignite.Abp.BlobManagement](https://github.com/dignite-projects/abp-modules/tree/main/blob-management),
an ABP Framework module that manages a tenant's BLOB containers. This library holds the module's management pages,
for users who hold the `BlobManagement.Containers` permission: the **Usage** page shows what the tenant stores (by
container, user, client and, in the host, by tenant), and the **Files** page shows one container at a time. On the Files
page users manage the container's directories, and its files are read-only.

Route the pages with `createRoutes()` from `@dignite/ng.blob-management`
(`{ path: 'blob-management', loadChildren: () => import('@dignite/ng.blob-management').then(m => m.createRoutes()) }`)
and add the menu items with `provideBlobManagementConfig()` from `@dignite/ng.blob-management/config`.

See the [Blob Management documentation](https://github.com/dignite-projects/abp-modules/tree/main/docs/en/blob-management)
for installation and configuration.
