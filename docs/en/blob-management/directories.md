```json
//[doc-seo]
{
    "Description": "Directories in Dignite Blob Management: containers that allow them, the directory tree, listing a container's files with filter, sorting and paging, renaming, moving and deleting files and directories, and the HTTP API."
}
```

# Directories

> Blob Management has not been released yet; see the [overview](index.md).

A directory is only the hierarchy of the files people upload. A container has directories when the host allows them (`AllowDirectories`, [Managed Containers](setting-up.md#managed-containers)); the default is no. Without them, a container's files are one flat list, every directory operation is refused (`BlobManagement:DirectoriesNotAllowed`), and a file there has no directory.

The **top of the container** is not a directory: files and directories are put there as well as in directories. A file an application saved under its name always stays at the top.

## The Tree

A container that allows directories holds one tree for each tenant.

* A name is unique among the directories beside it, **compared without regard to case** (`Reports` and `reports` cannot both be there), and is at most 256 characters long.
* A tree is at most 16 directories deep.
* The directories beside each other have an **order**: a new one goes last, and a move names the position.
* A directory moves within its own container only, never into itself or below itself. Its files and subdirectories go with it.
* Only an **empty** directory is deleted; one that still holds files or directories is refused (`BlobManagement:DirectoryNotEmpty`). A deleted directory is kept as a deleted row, and the grants on it are deleted with it.

| | |
|---|---|
| `GET api/blob-management/directories?containerName=&parentId=` | The directories directly under `parentId`, or at the top of the container without it, that the caller may see, in their order. |
| `GET api/blob-management/directories/{id}` | One directory. |
| `POST api/blob-management/directories` | Creates one: `containerName`, `parentId` (none for the top), `name`. |
| `PUT api/blob-management/directories/{id}` | Renames it: `name`. |
| `PUT api/blob-management/directories/{id}/move` | Moves it: `parentId` (none for the top of the container) and `order`, its position there; the directories at or after it move down by one. |
| `PUT api/blob-management/directories/{id}/restriction` | Says whether it has permissions of its own: `isRestricted` ([Permissions](permissions.md#restricted-directories)). |
| `DELETE api/blob-management/directories/{id}` | Deletes an empty directory. Deleting one that does not exist succeeds. |

A directory (`BlobManagementDirectoryDto`) has its `id`, `containerName`, `name`, `parentId`, `order`, `hasChildren`, `isRestricted`, its creation time and creator, and what the caller may do with it: `canUpdate` (rename and move it), `canDelete`, and `access`, what the caller may do inside it.

## Listing Files

`GET api/blob-management/files` lists the files of one directory, or of the top of the container, a page at a time:

| Parameter | |
|---|---|
| `containerName` | The container. Required. |
| `directoryId` | The directory; none for the top of the container. |
| `filter` | A part of the file name, whatever its case. |
| `sorting` | `Name`, `Size`, `MimeType`, `CreationTime` or `LastModificationTime` (the case does not matter), then `asc` or `desc`. Default: `Name asc`. Anything else is refused (`BlobManagement:SortingNotSupported`). |
| `skipCount`, `maxResultCount` | Paging, as everywhere in ABP. |

The listing needs the permission to view the place ([Permissions](permissions.md)), and then shows every file there: whether a file may be renamed, moved or deleted is the place's answer, which the caller reads from the container's or the directory's `access`. A file with `isAddressedByName` is never renamed or moved, and a file the caller uploaded may always be deleted by them.

In a container without directories the same call, without `directoryId`, lists the whole container.

## Uploading Into a Directory

`POST api/blob-management/files` takes a `directoryId`, the directory to file the file in; none is the top of the container. So do [upload links](attaching-files.md#upload-links-for-users). Everything else is the same upload: the type detected from the bytes, the container's limits, de-duplication, the tenant quota.

## Renaming and Moving a File

| | |
|---|---|
| `PUT api/blob-management/files/{id}` | Renames the file: `name`. Names may repeat. |
| `PUT api/blob-management/files/{id}/move` | Moves it to `directoryId`, in the same container; none for the top of the container. |

**A file never changes containers**: a directory of another container is not found. A file an application saved under its name is neither renamed nor moved (`BlobManagement:FileAddressedByName`). Moving a file does not change its public address.

### Moving a File

A move takes the permission to rename and move files where the file is, and the permission to upload where it lands.

## From C#

The services are in `Dignite.Abp.BlobManagement.Application.Contracts` - in process with `Dignite.Abp.BlobManagement.Application`, or remote with `Dignite.Abp.BlobManagement.HttpApi.Client`:

* `IBlobManagementDirectoryAppService`: `GetAsync`, `GetListAsync`, `CreateAsync`, `UpdateAsync` (rename), `MoveAsync`, `DeleteAsync`, `RestrictAsync`.
* `IBlobManagementFileAppService`: `GetAsync`, `GetListAsync`, `CreateAsync`, `CreateUploadTicketAsync`, `UpdateAsync` (rename), `MoveAsync`, `DeleteAsync`, `GetContentAsync`.

## Errors

| Code | |
|---|---|
| `BlobManagement:DirectoriesNotAllowed` | The container does not allow directories (HTTP 400). |
| `BlobManagement:DirectoryNotFound` | The directory does not exist where it was named, for example in another container (HTTP 404). |
| `BlobManagement:DirectoryNameAlreadyExists` | A directory beside it has the name already, compared without regard to case (HTTP 409). |
| `BlobManagement:DirectoryCannotMoveIntoItself` | A directory cannot be moved into itself or below itself (HTTP 400). |
| `BlobManagement:DirectoryTooDeep` | The tree would be deeper than 16 directories (HTTP 400). |
| `BlobManagement:DirectoryNotEmpty` | Only an empty directory can be deleted (HTTP 409). |
| `BlobManagement:SortingNotSupported` | A file listing was asked to sort by something it does not sort by (HTTP 400). |
| `BlobManagement:FileAddressedByName` | The file is not renamed or moved (HTTP 409). |

## See Also

* [Permissions](permissions.md)
* [Attaching Files](attaching-files.md)
* [Importing a Directory Tree](importing-directories.md)
