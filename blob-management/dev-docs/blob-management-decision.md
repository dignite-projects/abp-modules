# Blob Management: the redesign of 2026-11-12

This record replaces the direction of [`drive-decision.md`](drive-decision.md) (kept as history, with its own names)
and of Drive Pro's record in the commercial repository. What those two decided about the storage tier - descriptors,
content dedup, the tenant allocation, public reads with sizing, tickets, the BLOB API and its client list - stands
unless something here says otherwise.

## Decided by the owner, 2026-11-12

Kept close to his words.

- **The module was named wrong.** "Drive" steers everyone toward a cloud drive. It is **Blob Management**: a BLOB
  management module, an ABP infrastructure module and not an application module.
- **There is no Pro edition.** Everything is in this repository; `drive/` became `blob-management/`.
- **It is the tenant's BLOB containers, managed.** No personal drives. Whoever holds the management permission enters
  the back office and sees every container: a high, top-level permission.
- **A directory is only the hierarchy of files.**
- **The concepts "system container" and "user drive" are removed.**
- **Permissions are configured on directories**: the roles and users that have them create, edit, delete and view
  subdirectories.
- **What a user may do with files - upload, delete, rename and the like - is the BLOB container's permission.** ABP
  has no permission on a container (checked: `Volo.Abp.BlobStoring` 10.7.0 has no authorization at all), so the
  module defines it; how is under "Proposed" below.
- **Directories are built on a container, and each container says whether it has them; the default is no.** Only a
  container that allows them has a directory tree, in the back office and in the file explorer a business system opens.
- **Containers serve business systems, so sharing a file is removed.** ABP's File Management has sharing, rightly; this
  module does not compete with it.

## Proposed by the implementing session

The owner left for the day with "redesign it and carry it out; do not ask". Each item is a choice his words leave open.

### Names

| Was | Is |
|---|---|
| `drive/`, `docs/en/drive/`, `docs/en/drive-pro/` | `blob-management/`, `docs/en/blob-management/` |
| `Dignite.Drive.*` | `Dignite.Abp.BlobManagement.*` (as `Dignite.Abp.FlexFields`, `Dignite.Abp.Notifications`) |
| `Dignite.Abp.BlobStoring.Drive`, `UseDrive(...)` | `Dignite.Abp.BlobStoring.BlobManagement`, `UseBlobManagement(...)` |
| `UseDriveContainer(...)` | `UseBlobManagementContainer(...)` |
| a type named `Drive…` | the same type named `BlobManagement…` |
| `@dignite/ng.drive`, `@dignite-projects/ng.drive.pro` | `@dignite/ng.blob-management` |
| `api/drive`, remote service and module `Drive` / `drive` | `api/blob-management`, `BlobManagement` / `blobManagement` |
| permission and feature group, configuration section `Drive` | `BlobManagement` |
| table prefix `Drive`, connection string `Drive` | `Blob`, `BlobManagement` |
| MCP module `drive`, tools `drive_*` | `blob`, `blob_*` |

`Drive` became `BlobManagement` in every type name, without exception: a shorter `Blob…` collides with ABP's own
(`BlobContainerConfiguration`). Nothing was published under the old names, so this is no rename for a consumer.

### Containers

One kind. A container is managed once the host declares it (`UseBlobManagementContainer`, or the `BlobManagement`
configuration section), with `PublicRead`, `AllowedClientIds` and `AllowDirectories` (default `false`). `Kind` is gone
from code and configuration.

### Files: one table, two ways in

Removing the two kinds of container does not remove the two ways a file arrives, which are the two interfaces of a
BLOB store with people in front of it:

- **An application saves a BLOB under a name** (`IBlobContainer`, the BLOB API, client tickets). The name is its
  address: unique among the container's live files, held by the `NameKey`, and the application reads it back by it.
- **A person uploads a file** (the file API, the explorer, the file input, a user's upload link). It is addressed by
  its id; its name is a display name and may repeat.

Both are rows of the one table, in any managed container, and the back office lists both. The difference is a
property of the file, not of the container: `FileDescriptor.IsAddressedByName`. Such a file is not renamed or moved -
its name is how its application finds it (`BlobManagement:FileAddressedByName`) - and lists at the top of its
container; it is read, downloaded and deleted like any other. The public address of a file is its name when it is
addressed by name, else its physical BLOB name, so renaming or moving a file never breaks a published link.

Not chosen: one namespace in which a directory path plus a name is the address of every file. It would make every
BLOB read of an application resolve a directory chain, and forbid the repeating names people upload.

### Directories

`DirectoryDescriptor`, in the module's own table, as before the redesign minus the owner: a parent, a position, an id
path, a name unique among live siblings whatever its case (the unique `NameKey`), at most 16 levels, deleted only when
empty, soft-deleted. A file's `DirectoryId` is a property of `FileDescriptor` (a column, a field), no longer an ABP
object extension. A container without `AllowDirectories` refuses every directory operation
(`BlobManagement:DirectoriesNotAllowed`) and a file there has no directory.

### Authorization

ABP's resource permissions answer one question about one resource. The design asks them only that: per request, one
check on the container and one per directory that has permissions of its own - never one per file.

- **Module-wide (roles, ABP's permission dialog)**, group `BlobManagement`:
  `BlobManagement.Containers` - the management permission: enter the back office, see every container with its
  directories and files - with the children `.UploadFiles`, `.UpdateFiles` (rename, move), `.DeleteFiles`,
  `.CreateDirectories`, `.UpdateDirectories` (rename, move), `.DeleteDirectories`; and
  `BlobManagement.ManagePermissions`, which manages the grants below.
- **On one container** (resource `Dignite.Abp.BlobManagement.BlobManagementContainer`, key: its name), granted to a
  role or a user in ABP's resource permission dialog: `BlobManagement.Container.View`, `.UploadFiles`,
  `.UpdateFiles`, `.DeleteFiles`, `.CreateDirectories`, `.UpdateDirectories`, `.DeleteDirectories`. A grant is the
  module-wide permission limited to that container.
- **On one directory** (resource `Dignite.Abp.BlobManagement.DirectoryDescriptor`, key: its id):
  `BlobManagement.Directory.View`, `.CreateDirectories`, `.UpdateDirectories`, `.DeleteDirectories`. They count only
  while the directory is **restricted** (`DirectoryDescriptor.IsRestricted`, switched by a holder of
  `ManagePermissions`): a restricted directory, with everything below it, is seen and its subdirectories managed only by
  those granted on it and by holders of the module-wide permissions - "the roles and users that have the permission".
  A directory that is not restricted follows the nearest restricted directory above it, or the container.

**A place** is the top of a container or a directory, and its **scope** is the nearest restricted directory at or
above it, else the container. What a caller may do at a place:

| | Scope is the container | Scope is a restricted directory |
|---|---|---|
| View (list its directories and files) | `Containers` or `Container.View` | `Containers` or `Directory.View` |
| Upload, rename and move, delete files | View, and the module-wide or the container's permission | View, and the module-wide or the **container's** permission |
| Create, rename and move, delete subdirectories | View, and the module-wide or the container's permission | View, and the module-wide or the **directory's** permission |

So every row of a listing shares the place's answer, and a listing asks the grant store once for the place and once
for each restricted subdirectory at that level (to hide the ones the caller may not view). The flag is a column, not
"has any grant", because whether a directory has grants is itself a question per directory.

Further rules:

- A directory is renamed, moved and deleted by the permission of the place it is in, by a caller who may view it. A
  move also takes creating directories where it lands. A restricted directory keeps its restriction where it goes.
- A file is moved by renaming-and-moving where it is and uploading where it lands.
- **The uploader may delete their own file**, as before: an application's cleanup after a save relies on it.
- **Reading one file by its id takes a signed-in user and nothing else**, as in the open edition before: the id is what
  a business system keeps and hands out under its own authorization. Listing is what the permissions govern. A
  `PublicRead` container stays anonymously readable by address.
- Every signed-in user may read one container's limits by its name (the file input needs them); the list of containers
  is those the caller holds the management permission or any grant for.
- Only users are authorized here. Clients go through the BLOB API and its `AllowedClientIds`, now on any container.

Not chosen: bridging a container to a business system's own permission names in its configuration (rejected in
`drive-decision.md` for making policy names a cross-service contract; a container grant gives a business role the same
thing); grants on a file, ownership, and the creator's rights over what they created (sharing, removed); deriving
"restricted" from the existence of grants or from events of the grant store (a question per directory; and no events
when Permission Management runs in another service).

### What is removed

Personal drives (the scope, the owner field, their configuration section and import), per-file grants, the creator's
rules, the replaced services and the replaceable file input (one edition has nothing to replace), the object-extension
plumbing and the replaced MongoDB model source, the trash and share links that were planned.

### Surface

- `IBlobManagementContainerAppService`: the containers the caller may see, one container with its limits and what
  the caller may do at its top.
- `IBlobManagementFileAppService`: list by container and directory, one file, its bytes, upload (into a directory or
  the top), an upload link, rename, move, delete.
- `IBlobManagementDirectoryAppService`: list a level, one directory, create, rename, move, delete, restrict.
- The BLOB API, client tickets, ticket redemption and public reads, on any managed container.
- MCP module `blob`: `blob_list_containers`, `blob_list_directories`, `blob_create_directory`, `blob_list_files`,
  `blob_get_file`, `blob_create_upload_url`, `blob_upload_file`, `blob_update_file`, `blob_delete_file`.
- `@dignite/ng.blob-management`: the management page (route guarded by `BlobManagement.Containers`), the explorer and
  the file picker a business system opens, and the file input, which uploads and picks.
- `DirectoryPathImporter` stays, for an application that migrates its own file library in.

## Still open

- The site repository (`feat/drive-integration`) consumes the packages under their old names from `_local-feed`; it
  moves to these names in a change of its own.
- The licence text of the commercial repository, and what that repository is for now that Drive Pro is gone.
- A native speaker's review of the texts in the 23 cultures other than English and Simplified Chinese.
- Publishing.
