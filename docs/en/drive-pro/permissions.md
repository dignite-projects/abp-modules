```json
//[doc-seo]
{
    "Description": "Dignite Drive Pro permissions: the module-wide Drive permissions it adds, per-item grants on a drive, a directory or a file, ownership and creation, who may do what, and why reading a file by its id needs permission to view it."
}
```

# Permissions

> Drive Pro has not been released yet; see the [overview](index.md).

Drive Pro authorizes everything people do in the user drives - the open [file API](../drive/attaching-files.md) as well as the [file library](file-library.md) - with one set of rules, in place of the [open Drive's](../drive/permissions.md). There are three kinds of access: **module-wide permissions**, **per-item grants**, and what **owning or creating** an item gives.

## Module-Wide Permissions

In the `Drive` group of ABP's permission management, beside the open Drive's:

| Permission | |
|---|---|
| `Drive.Files` | The open parent; it grants nothing by itself. |
| `Drive.Files.Upload` | (Open.) Upload files anywhere. |
| `Drive.Files.Delete` | (Open.) Delete every file. |
| `Drive.Files.View` | See every file. |
| `Drive.Files.Update` | Rename and move every file. |
| `Drive.Directories` | The parent of the four below; it grants nothing by itself. |
| `Drive.Directories.View` | See every directory and everything in it. |
| `Drive.Directories.Create` | Create directories anywhere. |
| `Drive.Directories.Update` | Rename and move every directory. |
| `Drive.Directories.Delete` | Delete every (empty) directory. |
| `Drive.ManagePermissions` | Manage the per-item grants of every drive, directory and file. An administrator's permission: whoever holds it can grant themselves access to anything. |

They act on every item of a **tenant-wide** drive, and in a [personal drive](personal-drives.md) on the user's own items only: no module-wide permission opens another user's personal drive.

Seeing every file is `Drive.Files.View`, not the parent `Drive.Files`: ABP's permission dialog checks a parent with its child, so every role that may upload or delete already holds `Drive.Files`.

## Per-Item Grants

A [resource permission](https://abp.io/docs/latest/modules/permission-management) is granted on **one** drive, directory or file - to a role or a user, as ABP's resource permission dialog offers - by a holder of `Drive.ManagePermissions`. The [Drive page](angular-ui.md#the-drive-page) opens the dialog for the item:

| On | Resource name | Permissions |
|---|---|---|
| A drive, by its name | `Dignite.Drive.Pro.DriveContainer` | `Drive.Container.Upload` (upload anywhere in it), `Drive.Container.CreateDirectory` (create directories anywhere in it) |
| A directory, by its id | `Dignite.Drive.Pro.DirectoryDescriptor` | `Drive.Directory.View` (see it and everything below it), `Drive.Directory.Add` (upload and create directories in it and everywhere below it), `Drive.Directory.Update` (rename and move it), `Drive.Directory.Delete` (delete it once empty) |
| A file, by its id | `Dignite.Drive.FileDescriptor` | `Drive.File.View`, `Drive.File.Update` (rename and move it), `Drive.File.Delete` |

A grant on a drive is the module-wide permission limited to that drive - in a personal drive, to the user's own items there. Grants to **view** and to **add** on a directory reach everything below it; the others apply to the item they are granted on.

Because the resource names are the classes' full names, `IAuthorizationService.IsGrantedAsync(file, "Drive.File.View")` answers with all of Drive Pro's rules, as the services do.

## Owning and Creating

* **The owner of a personal drive** may do anything with every item in it.
* **In a tenant-wide drive, the creator of an item** may view, rename, move and delete it, and upload and create directories directly in a directory they created - but sees nothing below it that others put there, unless something else lets them.
* **The uploader of a file** may view and delete it in any drive. The open Drive lets the uploader delete their own file, and that is kept.

## Who May Do What

Access to an item is the first of these that allows it: owning or creating it, a module-wide permission, a grant on it or above it.

| | Allowed to |
|---|---|
| List the user drives | Every signed-in user. |
| See a directory, its subdirectories and its files | `Drive.Directories.View`, or `Drive.Directory.View` on it or above it. Its creator sees it, with what they put in it. |
| See a file, and read it by its id | `Drive.Files.View`, `Drive.File.View` on it, `Drive.Directory.View` above it, or having uploaded it. |
| Upload, or create a directory, at the top of a drive | `Drive.Files.Upload` / `Drive.Directories.Create`, or the drive's grant - in a personal drive too: the top of a drive is nobody's item. |
| Upload, or create a directory, in a directory | `Drive.Files.Upload` / `Drive.Directories.Create`, or `Drive.Directory.Add` on it or above it. |
| Rename a file | `Drive.Files.Update` or `Drive.File.Update`. |
| Move a file, or a directory | Renaming it, and adding where it goes. |
| Delete a file | `Drive.Files.Delete`, `Drive.File.Delete`, or having uploaded it. |
| Rename or delete a directory | `Drive.Directories.Update` / `.Delete`, or `Drive.Directory.Update` / `.Delete` on it. |
| Manage grants | `Drive.ManagePermissions`. |

The owner of a personal drive is allowed all of these on their own items, and the creator of an item in a tenant-wide drive those on that item, as above. A token without a user is allowed none of them, and nobody is while the tenant's Drive is off ([The Drive Feature](../drive/attaching-files.md#the-drive-feature)).

## Reading a File by Its Id

**This is the one place Drive Pro is stricter than the open Drive.** In the open Drive any signed-in user who has a user-drive file's id may read it; with Drive Pro, reading a file - its details or its bytes - needs permission to see it, as above.

An application that shows users the files of a user drive by their ids - an article's attachments, say - therefore loses them for users who may not see them. Granting `Drive.Files.View` to the role every user has restores the open behaviour in the tenant-wide drives; a [personal drive](personal-drives.md)'s files stay their owners' (and those they are granted to). Files of a `PublicRead` drive are not affected: anyone reads them at their public address, as in the open Drive.

## What It Needs

ABP's [Permission Management](https://abp.io/docs/latest/modules/permission-management) with its resource permissions, in the host that runs the Drive; the Angular resource permission dialog comes from `@abp/ng.permission-management`.

## See Also

* [The File Library](file-library.md)
* [Personal Drives](personal-drives.md)
* [Permissions in the open Drive](../drive/permissions.md)
