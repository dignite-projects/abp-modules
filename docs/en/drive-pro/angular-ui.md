```json
//[doc-seo]
{
    "Description": "The Dignite Drive Pro Angular UI (@dignite-projects/ng.drive.pro): the Drive page with its directory tree and file list, a file picker, and a file input registered in place of the open Drive's so every drive-file-input picks from the library."
}
```

# Angular UI

> Drive Pro has not been released yet; see the [overview](index.md).

`@dignite-projects/ng.drive.pro` brings the [file library](file-library.md) to ABP's Angular UI: the **Drive page**, a **file picker**, and a **file input** that takes the place of the open Drive's, so that every `drive-file-input` picks from the library as well as uploading. It builds on the open Drive's `@dignite/ng.drive` ([Angular UI](../drive/angular-ui.md)), which it takes as a peer dependency.

## Installation

Add `@dignite-projects/ng.drive.pro` and `@dignite/ng.drive` to the application. The package comes from GitHub Packages; the application's `.npmrc` names the registry for the scope, with a token in an environment variable, never in the file:

````
@dignite-projects:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${GITHUB_PACKAGES_TOKEN}
````

Its other peer dependencies are packages an ABP application has: `@abp/ng.core`, `@abp/ng.theme.shared`, `@abp/ng.components`, `@abp/ng.permission-management` (the resource permission dialog), `@ng-bootstrap/ng-bootstrap`, `@swimlane/ngx-datatable`, `@ngx-validate/core` and Angular itself. **Install `@dignite/ng.drive` once**, at the root of `node_modules`: the replacement below is registered against the open package's key, and with two copies of it the forms keep the open file input.

Register the menu item and the file input in `app.config.ts`, and the page in `app.routes.ts`:

````ts
import { provideDriveProFileInput } from '@dignite-projects/ng.drive.pro';
import { provideDriveConfig } from '@dignite-projects/ng.drive.pro/config';

export const appConfig: ApplicationConfig = {
  providers: [
    // ...
    provideDriveConfig(),
    provideDriveProFileInput(),
  ],
};
````

````ts
export const APP_ROUTES: Routes = [
  // ...
  {
    path: 'drive',
    loadChildren: () => import('@dignite-projects/ng.drive.pro').then(m => m.createRoutes()),
  },
];
````

The proxies call the API named `Drive`, as the open ones do: `apis.Drive` in the environment when the Drive runs as [its own service](../drive/hosting.md#the-drive-as-its-own-service), `apis.default` otherwise.

## The Drive Page

The page, at `/drive` for signed-in users, shows the user drives one at a time as tabs - a [personal drive](personal-drives.md) as the user's own. Each drive shows its **directory tree** beside the **files** of the directory chosen:

* the tree loads a level when it is opened; directories are created, renamed and deleted from a node's menu, and moved by dragging;
* the file list pages, searches and sorts the files, uploads into the directory shown, and downloads, renames and deletes a file; a picture of a public drive is shown as a thumbnail;
* a holder of `Drive.ManagePermissions` opens the resource permission dialog for a drive, a directory or a file ([Permissions](permissions.md#per-item-grants)).

Each action is offered only where the user may take it, from the permissions the API sends with each item. Moving a file is done through the API ([The File Library](file-library.md#renaming-and-moving-a-file)), not the page.

The page is a [replaceable component](https://abp.io/docs/latest/framework/ui/angular/component-replacement) under `eDriveProComponents.Drive` (`'Drive.DriveComponent'`). `provideDriveConfig()` adds the menu item (`eDriveRouteNames.Drive`); leave it out to place the page elsewhere.

## The File Input

`provideDriveProFileInput()` registers Drive Pro's file input under the open key `eDriveComponents.FileInput`. Every `drive-file-input` of the application - the forms are unchanged - then shows two buttons: **Upload**, as the open input does (to the top of the drive, in a personal drive the user's own), and **Select files**, which opens the file picker on the drive the input names. Its value is still the list of `DriveFileDto`, bound to the form through the open component; removing a file only takes it out of the value.

## The File Picker

For a form without the open input, `drive-file-picker` chooses files from the library: a button that opens the picker and the files chosen, each removable, bound with `[(value)]`:

````html
<drive-file-picker containerName="shared" [multiple]="true" [(value)]="files" />
````

````ts
import { DriveFilePickerComponent } from '@dignite-projects/ng.drive.pro';
````

The picker is a modal showing what the Drive page shows - browsing, uploading and the rest - plus a choice that is kept across directories and pages; the choice is handed over when it is confirmed. Without `containerName` it shows every user drive. The modal alone is `drive-file-picker-modal` (`[(visible)]`, `containerName`, `multiple`, `value`, and `(confirmed)` with the files chosen).

## Other Pieces

The library also exports the proxies of the library's API (`DriveDirectoryService`, `DriveFileLibraryService` and their DTOs), the page's components (`drive-explorer`, `drive-browser`, `drive-directory-tree`, `drive-file-list`, `drive-file-preview`), and helpers for what Drive Pro adds to the open DTOs: `getFileDirectoryId`, `getFileOwnerId`, `getFileResourcePermissions`, `getContainerScope` and `getContainerResourcePermissions`, with `eDrivePermissions` naming the resource permissions. The open services, pipes and URL services come from `@dignite/ng.drive`.

## See Also

* [The File Library](file-library.md)
* [Permissions](permissions.md)
* [Angular UI of the open Drive](../drive/angular-ui.md)
