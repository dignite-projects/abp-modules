```json
//[doc-seo]
{
    "Description": "The Dignite Blob Management Angular UI (@dignite/ng.blob-management): the management pages for administrators - the tenant's usage (by tenant, for the host), and one container at a time with its directory tree and its files, read-only."
}
```

# Angular UI

> Blob Management has not been released yet; see the [overview](index.md).

`@dignite/ng.blob-management` brings Blob Management's **management page** to ABP's Angular UI: a page for administrators - holders of `BlobManagement.Containers` - to look at what the tenant stores. It is not for ordinary users, and stores, renames, moves and deletes no file: a business system builds its own file UI and calls the [file service](files.md) from its server.

## Installation

Add `@dignite/ng.blob-management` to the application. Its peer dependencies are packages an ABP application has: `@abp/ng.core`, `@abp/ng.theme.shared`, `@abp/ng.components`, `@abp/ng.permission-management` (the resource permission dialog), `@swimlane/ngx-datatable`, `@ngx-validate/core` and Angular itself.

Register the page's menu item in `app.config.ts`, and its route in `app.routes.ts`:

````ts
import { provideBlobManagementConfig } from '@dignite/ng.blob-management/config';

export const appConfig: ApplicationConfig = {
  providers: [
    // ...
    provideBlobManagementConfig(),
  ],
};
````

````ts
export const APP_ROUTES: Routes = [
  // ...
  {
    path: 'blob-management',
    loadChildren: () => import('@dignite/ng.blob-management').then(m => m.createRoutes()),
  },
];
````

The proxies call the API named `BlobManagement`. Without an `apis.BlobManagement` entry in the environment, ABP uses `apis.default`, which is right when Blob Management is installed in the application's own host; when it runs as [its own service](hosting.md#blob-management-as-its-own-service), point `apis.BlobManagement.url` at it (or at the gateway). Public file addresses are completed with the same URL.

## The Pages

There are two pages, for holders of `BlobManagement.Containers` ([Permissions](permissions.md)): `provideBlobManagementConfig()` adds a **Blob Management** menu item under Administration (`eBlobManagementRouteNames.BlobManagement`) with **Usage** (`/blob-management/usage`) and **Files** (`/blob-management/files`) below it, and `createRoutes()` guards both by the same permission. `/blob-management` opens Usage.

**Usage** is what the tenant stores ([Usage](usage.md)): its stored bytes against its allocation, then by container, by user or by client, and - for the host - **by tenant**.

* Every breakdown is read when it is asked for, never on its own: the containers' with the totals when the page opens, the others the first time their tab is selected, and each again when its **Refresh** button is pressed.
* The **Tenants** tab is there only for a caller who holds `BlobManagement.Containers.TenantUsages`, which is defined for the host side only. It lists each tenant - and the host's own files - with its stored bytes, files and allocation, and shows the share of the allocation in red from 90%.

**Files** is the managed containers, **one at a time** as tabs.

* A container that [allows directories](directories.md) shows its **directory tree** beside the **files** of the directory chosen; one without them shows its files as one list.
* The tree loads a level when it is opened. Directories are created, renamed and deleted from a node's menu, and moved by dragging, each offered only where the container's `access` allows it.
* The file list pages and searches the files by name, and downloads one. The search matches a part of the name, whatever its case, among the files of the container or directory shown - not those of its subdirectories. A public picture is shown as a thumbnail; a file uploaded with a ticket and not confirmed yet is not listed. Nothing else is done to a file here: it belongs to the business record that refers to it.
* A holder of `BlobManagement.ManagePermissions` opens ABP's resource permission dialog for the container shown.

Each page is a [replaceable component](https://abp.io/docs/latest/framework/ui/angular/component-replacement): Usage under `eBlobManagementComponents.Usage` (`'BlobManagement.BlobManagementUsageComponent'`), Files under `eBlobManagementComponents.Files` (`'BlobManagement.BlobManagementFilesComponent'`). Leave out `provideBlobManagementConfig()` to place the pages elsewhere.

## Its Parts

The pages are made of standalone components the library exports, for an application that composes its own page:

| Selector | |
|---|---|
| `blob-management-files` | The whole Files page: the container tabs, and the permissions dialog. |
| `blob-usage` | The whole Usage page: the tenant against its allocation, then by container, by user, by client or - in the host - by tenant. |
| `blob-browser` | One container (`[container]`): its directory tree beside its files, or one flat list. |
| `blob-directory-tree` | A container's directories (`[container]`), emitting the directory chosen (`(selectionChange)`). A caller who may not change the container's directories only chooses one - no menu, no dragging. |
| `blob-file-list` | The files of a container (`[container]`) or of one of its directories (`[directory]`), read-only, with download. |
| `blob-file-preview` | A thumbnail, or a type icon, for one file (`src`, `mimeType`, `name`). |

## Other Pieces

The library also exports:

* from `@dignite/ng.blob-management/proxy`, the proxies and their DTOs by name: `BlobManagementContainerService`, `BlobManagementDirectoryService`, `BlobManagementFileService` (the reads of the management page: a list, one file, its bytes), `BlobManagementUsageService`, `BlobManagementContainerDto`, `BlobManagementDirectoryDto`, `BlobManagementFileDto`, `BlobManagementAccessDto` and the usage DTOs. There is no proxy for storing or changing a file: the [BLOB API and the file API](files.md#the-file-api) are integration services for back ends, which a browser does not call;
* `BlobManagementFileUrlService` (`toAbsolute`, and `sized` for a public picture at one of the sizes its container serves, chosen with `toBlobManagementPublicSize` from the container's `publicSizes`) and the `blobFileSize` pipe;
* `eBlobManagementPolicyNames`, from `@dignite/ng.blob-management/config`, and `eBlobManagementResourceNames`: the permission and resource names the UI uses.

## See Also

* [Usage](usage.md)
* [Directories](directories.md)
* [Permissions](permissions.md)
