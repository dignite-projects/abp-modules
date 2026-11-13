# Blob Management: what it is for (2026-11-13)

This record narrows [`blob-management-decision.md`](blob-management-decision.md) (2026-11-12). Its sections on
authorization, on the surface and on the two ways a file arrives are replaced by what is written here; the names, the
containers, the directory tree's own rules and everything [`drive-decision.md`](drive-decision.md) decided about the
storage tier stand.

## Decided by the owner, 2026-11-13

Kept close to his words.

- **It is a basic function of the system, in the service of business systems.** It counts: the capacity a tenant
  occupies, the capacity each container occupies and, finer, the capacity each user uses. A directory is only a further
  classification of the BLOBs of a container.
- **It has no upload of its own.** A BLOB that no business system refers to has no value.
- **It is not shown to ordinary users.** Its page is for administrators - whoever holds the permission - to look at the
  data.
- **A business system builds its own UI** for uploading, deleting, renaming and moving files.
- **Creating, editing, deleting and moving directories is permitted here**: whoever holds the permission does it, so a
  business system need not build that part.
- **A business system reaches it as a client.** Tickets are gone: what a user may do is known to the business system,
  not to this module, so a business system that wants an upload link makes its own.
- **The host does not see every tenant's usage in this version.**
- **The names stay**: `blob-management/`, `FileDescriptor`, `DirectoryDescriptor`, the table prefix `Blob`. A
  descriptor is not the BLOB - several share one - and ABP reserves the `Abp` prefix for its own core modules.

## What it is

A BLOB storing provider with a ledger. An application stores bytes through it; the module writes one `FileDescriptor`
for each, and from that table come the tenant's allocation, the usage figures and the page an administrator reads.

## Who calls it

| Caller | Admitted by | Does |
|---|---|---|
| An application | In process: trusted. Over HTTP: an OAuth client the container allows (`AllowedClientIds`), with a token that carries no user | BLOBs by name (`IBlobManagementBlobService`), files by id (`IBlobManagementFileService`) |
| Anyone | The address of a file in a `PublicRead` container | Reads it, sized if it is a picture |
| A user | A directory permission, module-wide or on the container | Creates, renames, moves and deletes directories |
| An administrator | `BlobManagement.Containers` | Reads: usage, containers, directories, files and their bytes |

There is no API for an end user to store, change or remove a file, and none to read one without the management
permission. An application's rules run in the application, before it calls.

## Files: two interfaces of an application

Both ways in are an application's now.

- **A BLOB under a name** - ABP's `IBlobContainer` through the provider, or `IBlobManagementBlobService`. The name is
  the address, unique among the container's live files.
- **A file by its id** - `IBlobManagementFileService`: list a place, one file, its bytes, store (into a directory or
  the top), rename, move, delete. What a business system's file library calls, for its user.

`FileDescriptor.IsAddressedByName` still tells them apart, and a file addressed by name is still never renamed or
moved.

`IBlobManagementFileService` follows `IBlobManagementBlobService`: the in-process implementation is trusted and never
exposed over HTTP; `IBlobManagementFileAppService` is the same contract behind an authorization, at
`api/blob-management/files`. That endpoint admits two callers: an allowed client, for everything; a user who holds the
management permission, for the reads - so the administrator's page needs no second file service.

### The uploader

`FileDescriptor.CreatorId` is the user an upload is counted for, and nothing else: no right comes with it. In process
it is the application's current user, as ABP's auditing records it, unless the application names another. Over HTTP
the token carries no user, so the application names one: `creatorId` on a save, which `Dignite.Abp.BlobManagement.HttpApi.Client`
fills with the application's current user. The module believes it because it believes the client.

## Directories

The tree and its rules are unchanged. Authorization is the container's, for every directory in it:

- **See the tree**: `BlobManagement.Containers`, or `BlobManagement.Container.View` on the container.
- **Create, rename and move, delete**: the caller sees the tree and holds `BlobManagement.Containers.CreateDirectories`
  / `.UpdateDirectories` / `.DeleteDirectories`, or the same granted on the container.

Removed: a directory with permissions of its own (`IsRestricted`, the `Directory` resource permissions, the cleaner of
its grants) and with it authorization by place. A restricted directory hid its files from those not granted on it; a
file listing is an application's call now, which names no user, so the module could hide the directory and not the
files in it. Who sees which file is the business system's rule.

Removed with them: the file permissions (`UploadFiles`, `UpdateFiles`, `DeleteFiles`, module-wide and on a container)
and the uploader's right to delete their own file.

## Usage

`IBlobManagementUsageAppService`, behind the management permission, in the current tenant:

| Figure | Measure |
|---|---|
| The tenant, each container | **Stored bytes**: each physical BLOB once, however many descriptors share it. The containers add up to the tenant, since content is shared within a container only |
| Each user (`CreatorId`), each client (`ClientId`) | **Uploaded bytes**: the sizes of their descriptors. Two users who uploaded the same bytes are each counted for them, so these do not add up to the stored bytes |

The allocation (`BlobManagementFeatures.StorageSize`) stays the tenant's, checked against stored bytes. There is no
allocation for a container or a user.

A user is shown by name where the host has a user lookup (`IExternalUserLookupServiceProvider`, ABP's own seam);
without one, by id.

## What is removed

- Tickets, all three kinds: issuing, redeeming, `IBlobManagementTicketClient`, the upload link.
- The file API for users, the explorer, the file picker and the file input of `@dignite/ng.blob-management`.
- The MCP module (`Dignite.Abp.BlobManagement.Mcp`, the `blob_*` tools), whole. First the tools that write went, with
  the user-facing file API; then, the same day, the four that read. The module's callers are business systems and an
  administrator at a page - no assistant acting for a user is among them; what the tools still showed was files
  without the business record they belong to, which a business system's own MCP tools show with it; and in a host that
  reaches Blob Management remotely the file tools would have read as the application's client, not as the signed-in
  user. Nothing was published, so nothing is retired; a package can be added later, when there is something for an
  assistant to ask here - the usage, most likely.
- Deleting or changing a file from the administrator's page: a file belongs to the business record that refers to it.

## Surface

- `IBlobManagementBlobService`, `IBlobManagementFileService`: what an application calls.
- `IBlobManagementContainerAppService`: the containers the caller has something to do in; one container with its limits.
- `IBlobManagementDirectoryAppService`: list a level, one directory, create, rename, move, delete.
- `IBlobManagementUsageAppService`: the tenant and its containers; by user; by client.
- Public reads with sizing, on a `PublicRead` container.
- `@dignite/ng.blob-management`: the management page - usage, containers, the directory tree with its operations, the
  files of a place, read only.

## Not chosen

- **The module renamed File Management.** ABP's File Management has that name, its routes (`api/file-management`) and
  its permission group (`FileManagement`), and an application may install both.
- **The entities renamed after BLOBs.** `FileDescriptor` has a `Name` and a `BlobName`; a BLOB store has no
  directories.
- **The `Abp` table prefix.** ABP's best practices reserve it for ABP's core modules
  (`entity-framework-core-integration.md`, `mongodb-integration.md`); the notifications module here uses `Notif`.
- **Keeping the client tickets.** They let a browser exchange bytes with a remote Blob Management without passing the
  application. Without them, a business system in front of a remote Blob Management relays every non-public upload and
  download; in process nothing changes. In exchange there is no anonymous endpoint that redeems tickets, and no file
  uploaded with a ticket and then never referred to. A direct upload can only be built in this module - only it can
  verify one - so it returns here if a remote deployment with large files needs it.
- **A trusted way for an application to write directories.** It would pass by the permissions that were just put here.

## Still open

- The site repository (`feat/drive-integration`) moves to these names and to `IBlobManagementFileService`.
- A component library shared by business systems' file UIs: judged when a second one has the same shape.
- A native speaker's review of the texts in the 23 cultures other than English and Simplified Chinese.
- Publishing.
