```json
//[doc-seo]
{
    "Description": "Directories in Dignite Blob Management: containers that allow them, the rules of the directory tree, who may create, rename, move and delete directories, and the directory HTTP API."
}
```

# Directories

> Blob Management has not been released yet; see the [overview](index.md).

A directory is a further classification of a container's files kept by id. A container has directories when the host allows them (`AllowDirectories`, [Managed Containers](setting-up.md#managed-containers)); the default is no. Without them, a container's files are one flat list, every directory operation is refused (`BlobManagement:DirectoriesNotAllowed`), and a file there has no directory.

The **top of the container** is not a directory: files and directories are put there as well as in directories. A file an application saved under its name always stays at the top.

Directories are the one thing Blob Management lets a user change, so that a business system need not build it: a user with the permission creates, renames, moves and deletes them, here or in the business system's own UI through the API below. Files are filed into a directory, and moved between directories, by the business system through the [file service](files.md).

## The Tree

A container that allows directories holds one tree for each tenant.

* A name is unique among the directories beside it, **compared without regard to case** (`Reports` and `reports` cannot both be there), and is at most 256 characters long.
* A tree is at most 16 directories deep.
* The directories beside each other have an **order**: a new one goes last, and a move names the position.
* A directory moves within its own container only, never into itself or below itself. Its files and subdirectories go with it.
* Only an **empty** directory is deleted; one that still holds files or directories is refused (`BlobManagement:DirectoryNotEmpty`). A deleted directory is kept as a deleted row.

## Who May Change It

Authorization is the container's, for every directory in it ([Permissions](permissions.md)):

* **See the tree**: `BlobManagement.Containers`, or `BlobManagement.Container.View` granted on the container.
* **Create**, **rename and move**, **delete**: see the tree, and hold `BlobManagement.Container.CreateDirectories`, `.UpdateDirectories` or `.DeleteDirectories` granted on the container. No module-wide permission changes directories.

What the caller may do is sent with each container as `access` (`view`, `createDirectories`, `updateDirectories`, `deleteDirectories`), so a UI offers each action only where it is allowed. A token without a user is allowed none of these.

## The API

`IBlobManagementDirectoryAppService`, in `Dignite.Abp.BlobManagement.Application.Contracts` - in process with `Dignite.Abp.BlobManagement.Application`, or remote with `Dignite.Abp.BlobManagement.HttpApi.Client`:

| | |
|---|---|
| `GET api/blob-management/directories?containerName=&parentId=` | `GetListAsync`: the directories directly under `parentId`, or at the top of the container without it, in their order. |
| `GET api/blob-management/directories/{id}` | `GetAsync`: one directory. |
| `POST api/blob-management/directories` | `CreateAsync`: `containerName`, `parentId` (none for the top), `name`. |
| `PUT api/blob-management/directories/{id}` | `UpdateAsync`: renames it, `name`. |
| `PUT api/blob-management/directories/{id}/move` | `MoveAsync`: `parentId` (none for the top of the container) and `order`, its position there; the directories at or after it move down by one. |
| `DELETE api/blob-management/directories/{id}` | `DeleteAsync`: deletes an empty directory. Deleting one that does not exist succeeds. |

A directory (`BlobManagementDirectoryDto`) has its `id`, `containerName`, `name`, `parentId`, `order`, `hasChildren`, and its creation time and creator.

The containers a user has something to do in, each with its `access`, are `GET api/blob-management/containers` (`IBlobManagementContainerAppService.GetListAsync`); one container by its name is `GET api/blob-management/containers/{name}`.

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
* [Keeping Files by Id](files.md)
* [Importing a Directory Tree](importing-directories.md)
