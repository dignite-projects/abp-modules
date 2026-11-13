```json
//[doc-seo]
{
    "Description": "The Dignite Blob Management Angular UI (@dignite/ng.blob-management): the management page, the file explorer and file picker a business system opens, and blob-file-input, a form control that uploads and picks files."
}
```

# Angular UI

> Blob Management has not been released yet; see the [overview](index.md).

`@dignite/ng.blob-management` brings Blob Management to ABP's Angular UI:

* the **management page**, the back office for holders of `BlobManagement.Containers`;
* the **explorer** and the **file picker** a business system opens on the container it keeps its files in;
* **`blob-file-input`**, a form control that uploads files and picks files already in the container;
* the proxies of the container, file and directory APIs.

## Installation

Add `@dignite/ng.blob-management` to the application. Its peer dependencies are packages an ABP application has: `@abp/ng.core`, `@abp/ng.theme.shared`, `@abp/ng.components`, `@abp/ng.permission-management` (the resource permission dialog), `@ng-bootstrap/ng-bootstrap`, `@swimlane/ngx-datatable`, `@ngx-validate/core` and Angular itself.

The file input, the explorer and the picker need nothing registered: import them where they are used. The management page is registered with its menu item in `app.config.ts`, and its route in `app.routes.ts`:

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

The page, at `/blob-management`, is for holders of `BlobManagement.Containers` ([Permissions](permissions.md)): `provideBlobManagementConfig()` adds its menu item under Administration (`eBlobManagementRouteNames.BlobManagement`), and the route is guarded by the same permission. It shows the managed containers one at a time as tabs. A container that [allows directories](directories.md) shows its **directory tree** beside the **files** of the directory chosen; one without them shows its files as one list.

* The tree loads a level when it is opened; directories are created, renamed and deleted from a node's menu, and moved by dragging.
* The file list pages and searches the files, uploads into the directory shown, and downloads, renames, moves and deletes a file - a move chooses the directory in a tree of the container's directories, offering those the user may upload to. A picture of a public container is shown as a thumbnail. A file an application saved under its name is not offered for renaming or moving.
* A holder of `BlobManagement.ManagePermissions` restricts a directory and opens ABP's resource permission dialog for a directory or a container.

Each action is offered only where the user may take it, from the `access` the API sends with each container and directory.

The page is a [replaceable component](https://abp.io/docs/latest/framework/ui/angular/component-replacement) under `eBlobManagementComponents.BlobManagement` (`'BlobManagement.BlobManagementComponent'`). Leave out `provideBlobManagementConfig()` to place the page elsewhere.

## The Explorer

`blob-explorer` is what the management page shows, for any place in the application: the containers the user has something to do in, as tabs, or only the one named by `containerName`. A business system opens it on the container it keeps its files in, for the users it has granted there ([Grants on a Container](permissions.md#grants-on-a-container)):

````html
<blob-explorer containerName="documents" />
````

`selection` (`'none'`, the default, `'single'` or `'multiple'`) lets files be chosen, kept across containers, directories and pages in `[(selectedFiles)]`.

## The File Picker

`blob-file-picker` chooses files from a container for a form: a button that opens the picker, and the files chosen, each removable, bound with `[(value)]`:

````html
<blob-file-picker containerName="documents" [multiple]="true" [(value)]="files" />
````

````ts
import { BlobManagementFileDto, BlobManagementFilePickerComponent } from '@dignite/ng.blob-management';
````

The picker is a modal showing what the explorer shows - browsing, uploading and the rest - plus a choice; the choice is handed over when it is confirmed. Without `containerName` it shows every container the user has something to do in. The modal alone is `blob-file-picker-modal` (`[(visible)]`, `containerName`, `multiple`, `value`, and `(confirmed)` with the files chosen).

## The File Input

`blob-file-input` attaches files to a form. Its value is the list of the files attached, each a `BlobManagementFileDto`; bind it as any form control, with `formControlName`, `[formControl]` or `ngModel`, or with `[(value)]` outside a form:

````html
<form [formGroup]="form">
  <blob-file-input formControlName="attachments" containerName="attachments" [multiple]="true" />
</form>
````

````ts
import { BlobManagementFileDto, BlobManagementFileInputComponent } from '@dignite/ng.blob-management';

@Component({
  // ...
  imports: [ReactiveFormsModule, BlobManagementFileInputComponent],
})
export class ArticleFormComponent {
  form = inject(FormBuilder).group({
    attachments: [[] as BlobManagementFileDto[]],
  });
}
````

* `containerName` is the container the files are uploaded to and picked from; without one the upload button is disabled.
* **Upload** sends the files chosen to the top of the container. **Select files** opens the file picker on the container, and is shown to a user who may view it.
* `multiple` lets several files be attached; the default is one, and an upload or a pick then replaces the file attached.
* The value is the list of `BlobManagementFileDto`. Store each file's `id`, or for a file of a public container its `url` (relative to the Blob Management host - `BlobManagementFileUrlService.toAbsolute` completes it).

The control reads the container's limits and checks the size and the type of each file chosen before sending it; the server checks again when it stores it, and authorizes the upload ([Permissions](permissions.md)). Files are uploaded one after another, and each is added to the value as soon as it is stored; one that fails does not stop the others. Disabling the form control disables the input.

**Removing a file only takes it out of the value.** The file stays in the container: the form may never be saved, and the value it replaces may still point to the file. To delete a file once the change is saved, call `BlobManagementFileService.delete` (the uploader may delete their own file; others need the permission to delete files there).

## Other Pieces

The library also exports:

* the proxies and their DTOs by name: `BlobManagementContainerService`, `BlobManagementFileService`, `BlobManagementDirectoryService`, `BlobManagementContainerDto`, `BlobManagementFileDto`, `BlobManagementDirectoryDto`, `BlobManagementAccessDto`, and `BlobManagementTicketDto` and `BlobManagementTicketKind` for `BlobManagementFileService.createUploadTicket`;
* the explorer's parts: `blob-browser`, `blob-directory-tree`, `blob-file-list`, `blob-file-move-modal` and `blob-file-preview`;
* `BlobManagementFileUrlService` (`toAbsolute`, and `sized` for a public picture at one of the sizes the host serves, `BLOB_MANAGEMENT_PUBLIC_SIZES`), `ObjectUrlService`, and the `blobFileSize` pipe;
* `eBlobManagementPolicyNames` and `eBlobManagementResourceNames`, the permission and resource names the UI uses.

## See Also

* [Attaching Files](attaching-files.md)
* [Directories](directories.md)
* [Permissions](permissions.md)
