```json
//[doc-seo]
{
    "Description": "The Dignite Drive Pro file library: directories in each user drive, listing a directory's files, uploading into a directory, renaming and moving files, what Drive Pro adds to the Drive's file and container DTOs, and the HTTP API."
}
```

# The File Library

> Drive Pro has not been released yet; see the [overview](index.md).

Drive Pro gives each user drive a **directory tree** and makes its files listable: a drive is browsed a directory at a time, files are uploaded into a directory, renamed and moved. The files are the Drive's own - the same file descriptors the open [file API](../drive/attaching-files.md) uploads, reads and deletes - and everything here is for signed-in users, under Drive Pro's [permissions](permissions.md).

## Directories

A user drive holds one tree, for the whole tenant; a [personal drive](personal-drives.md) holds one for each user. The **top of the drive** is not a directory: files and directories are put there as well as in directories.

* A name is unique among the directories beside it, **compared without regard to case** (`Reports` and `reports` cannot both be there), and is at most 256 characters long.
* A tree is at most 16 directories deep.
* The directories beside each other have an **order**: a new one goes last, and a move names the position.
* A directory moves within its own tree only, never into itself or below itself. Its files and subdirectories go with it.
* Only an **empty** directory is deleted; one that still holds files or directories is refused (`Drive:DirectoryNotEmpty`).

| | |
|---|---|
| `GET api/drive/directories?containerName=&parentId=` | The directories directly under `parentId`, or at the top of the drive without it (in a personal drive, the top of the caller's own), that the caller may see, in their order. |
| `GET api/drive/directories/{id}` | One directory. |
| `POST api/drive/directories` | Creates one: `containerName`, `parentId` (none for the top), `name`. |
| `PUT api/drive/directories/{id}` | Renames it: `name`. |
| `PUT api/drive/directories/{id}/move` | Moves it: `parentId` (none for the top of the tree) and `order`, its position there; the directories at or after it move down by one. |
| `DELETE api/drive/directories/{id}` | Deletes an empty directory. |

A directory (`DriveDirectoryDto`) has its `id`, `containerName`, `name`, `parentId`, `order`, `ownerId` (in a personal drive, the user whose drive holds it), `hasChildren`, its creation time and creator, and what the caller may do with it: `canUpload`, `canCreateDirectory`, and `resourcePermissions`, each of the directory's [resource permissions](permissions.md#per-item-grants) with whether the caller has it, every rule counted.

## Listing Files

`GET api/drive/files` lists the files of one directory, or of the top of the drive, a page at a time:

| Parameter | |
|---|---|
| `containerName` | The user drive. Required. |
| `directoryId` | The directory; none for the top of the drive (in a personal drive, the top of the caller's own). |
| `filter` | A part of the file name. |
| `sorting` | `name`, `size`, `mimeType`, `creationTime` or `lastModificationTime`, then `asc` or `desc`. Default: `name asc`. Anything else is refused (`Drive:SortingNotSupported`). |
| `skipCount`, `maxResultCount` | Paging, as everywhere in ABP. |

The listing shows only the files the caller may see ([Permissions](permissions.md)): at the top of a tenant-wide drive, for example, everyone's files with `Drive.Files.View`, and otherwise the caller's own uploads.

## Uploading Into a Directory

`POST api/drive/files/library` uploads a file as the open `POST api/drive/files` does - a form with `containerName`, an optional `name` and the file in `file` - plus `directoryId`, the directory to file it in (none for the top of the drive). It is authorized as adding to that directory: a grant to add there or above, `Drive.Files.Upload`, owning the personal drive, or - in a tenant-wide drive - having created the directory. Everything else is the open upload's: the type detected from the bytes, the drive's limits, de-duplication, the tenant quota.

The open upload, `POST api/drive/files`, and the open [upload links](../drive/attaching-files.md#upload-links-for-users) put a file at the top of the drive - in a personal drive, the top of the uploader's own. To file such a file in a directory, move it afterwards.

## Renaming and Moving a File

| | |
|---|---|
| `PUT api/drive/files/{id}` | Renames the file: `name`. Names may repeat, as everywhere in the Drive. |
| `PUT api/drive/files/{id}/move` | Moves it to `directoryId`, in the same tree; none for the top of the tree. |

**A file never changes drives**: it moves within its drive's tree - in a personal drive, within its owner's - and a directory elsewhere is not found (404). Reading and deleting a file stay the open API's (`GET`, `GET .../content` and `DELETE api/drive/files/{id}`).

## What a File Carries

The Drive's DTOs are [extensible](https://abp.io/docs/latest/framework/fundamentals/object-extensions), and Drive Pro sends what it adds in their `extraProperties`:

| DTO | Property | |
|---|---|---|
| `DriveFileDto` | `DriveProDirectoryId` | The directory the file is in; `null` at the top of its tree. |
| | `DriveProOwnerId` | In a personal drive, the user whose drive holds it; otherwise `null`. |
| | `DriveProResourcePermissions` | `Drive.File.View`, `Drive.File.Update` and `Drive.File.Delete`, each with whether the caller has it, every rule counted. Filled where the file is shown in the library; may be absent elsewhere. |
| `DriveContainerDto` | `DriveProScope` | Whose tree the drive holds: `0` (`Tenant`) or `1` (`Personal`). |
| | `DriveProResourcePermissions` | `Drive.Container.Upload` and `Drive.Container.CreateDirectory`, each with whether the caller may do it at the top of the drive. |

In C#, `DriveProDtoExtensions` reads them: `file.GetDirectoryId()`, `file.GetOwnerId()`, `file.GetResourcePermissions()`, `container.GetScope()` and `container.GetResourcePermissions()`. They are declared as object extensions, so the C# client proxies read them back with their types.

## From C#

The services are in `Dignite.Drive.Pro.Application.Contracts` - in process with `Dignite.Drive.Pro.Application`, or remote with `Dignite.Drive.Pro.HttpApi.Client`:

* `IDriveDirectoryAppService`: `GetAsync`, `GetListAsync`, `CreateAsync`, `UpdateAsync` (rename), `MoveAsync`, `DeleteAsync`.
* `IDriveFileLibraryAppService`: `GetListAsync`, `CreateAsync` (an upload into a directory), `UpdateAsync` (rename), `MoveAsync`.
* The open `IDriveFileAppService` and `IDriveContainerAppService` keep their members, with Drive Pro's implementations behind them.

## See Also

* [Personal Drives](personal-drives.md)
* [Permissions](permissions.md)
* [Attaching Files](../drive/attaching-files.md)
