```json
//[doc-seo]
{
    "Description": "The Dignite Blob Management Angular UI (@dignite/ng.blob-management): the management page for administrators - the tenant's usage, and one container at a time with its directory tree and its files, read-only."
}
```

# Angular UI

> Blob Management has not been released yet; see the [overview](index.md).

`@dignite/ng.blob-management` brings Blob Management's **management page** to ABP's Angular UI: a page for administrators - holders of `BlobManagement.Containers` - to look at what the tenant stores. It is not for ordinary users, and stores, renames, moves and deletes no file: a business system builds its own file UI and calls the [file service](files.md) from its server.

## Installation

Add `@dignite/ng.blob-management` to the application. Its peer dependencies are packages an ABP application has: `@abp/ng.core`, `@abp/ng.theme.shared`, `@abp/ng.components`, `@abp/ng.permission-management` (the resource permission dialog), `@ng-bootstrap/ng-bootstrap`, `@swimlane/ngx-datatable`, `@ngx-validate/core` and Angular itself.

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

## The Management Page

The page, at `/blob-management`, is for holders of `BlobManagement.Containers` ([Permissions](permissions.md)): `provideBlobManagementConfig()` adds its menu item under Administration (`eBlobManagementRouteNames.BlobManagement`), and the route is guarded by the same permission.

* **Usage** comes first: the tenant's stored bytes against its allocation, then by container, by user or by client ([Usage](usage.md)).
* Below it, the managed containers, **one at a time** as tabs. A container that [allows directories](directories.md) shows its **directory tree** beside the **files** of the directory chosen; one without them shows its files as one list.
* The tree loads a level when it is opened. Directories are created, renamed and deleted from a node's menu, and moved by dragging, each offered only where the container's `access` allows it.
* The file list pages and searches the files, and downloads one. A public picture is shown as a thumbnail; a file uploaded with a ticket and not confirmed yet is not listed. Nothing else is done to a file here: it belongs to the business record that refers to it.
* A holder of `BlobManagement.ManagePermissions` opens ABP's resource permission dialog for the container shown.

The page is a [replaceable component](https://abp.io/docs/latest/framework/ui/angular/component-replacement) under `eBlobManagementComponents.BlobManagement` (`'BlobManagement.BlobManagementComponent'`). Leave out `provideBlobManagementConfig()` to place the page elsewhere.

## Its Parts

The page is made of standalone components the library exports, for an application that composes its own page:

| Selector | |
|---|---|
| `blob-management-page` | The whole page. |
| `blob-usage` | The usage: the tenant against its allocation, then by container, by user or by client. |
| `blob-browser` | One container (`[container]`): its directory tree beside its files, or one flat list. |
| `blob-directory-tree` | A container's directories (`[container]`), emitting the directory chosen (`(selectionChange)`). With `[manage]="false"` it only chooses - no menu, no dragging. |
| `blob-file-list` | The files of a container (`[container]`) or of one of its directories (`[directory]`), read-only, with download. |
| `blob-file-preview` | A thumbnail, or a type icon, for one file (`src`, `mimeType`, `name`). |

## Other Pieces

The library also exports:

* the proxies and their DTOs by name: `BlobManagementContainerService`, `BlobManagementDirectoryService`, `BlobManagementFileService` (whose reads the page uses), `BlobManagementUsageService`, `BlobManagementContainerDto`, `BlobManagementDirectoryDto`, `BlobManagementFileDto`, `BlobManagementAccessDto` and the usage DTOs;
* `BlobManagementFileUrlService` (`toAbsolute`, and `sized` for a public picture at one of the sizes the host serves, `BLOB_MANAGEMENT_PUBLIC_SIZES`), `ObjectUrlService`, and the `blobFileSize` pipe;
* `eBlobManagementPolicyNames` and `eBlobManagementResourceNames`, the permission and resource names the UI uses.

## See Also

* [Usage](usage.md)
* [Directories](directories.md)
* [Permissions](permissions.md)
