```json
//[doc-seo]
{
    "Description": "Dignite Drive Pro: the commercial file library on the open Dignite Drive - directories, listing, rename and move, per-item grants, personal drives, the Drive page, a file picker and MCP tools for browsing."
}
```

# Drive Pro

> **Drive Pro is in development and has not been released yet.** It is released after the open [Drive](../drive/index.md), which it builds on. Its packages are not published.

Drive Pro is the **file library** of Dignite [Drive](../drive/index.md), a commercial module. The open Drive stores files and lets people attach them: a file is uploaded, kept by its id, read and deleted, and the Drive keeps no listing of a user drive. Drive Pro turns the user drives into a library people work in:

* **directories**, a tree per drive, created, renamed, moved and deleted;
* **listing** a drive's files, a directory at a time, with search, sorting and paging;
* **renaming** and **moving** files;
* **per-item grants**: a role, a user or a client is given access to one drive, one directory (and everything below it) or one file, in ABP's resource permission dialog;
* **personal drives**, where each user has a tree of their own;
* the **Drive page** and a **file picker** in the Angular UI, and a file input that picks from the library as well as uploading;
* **MCP tools** with which an AI assistant browses the drives and files what it uploads.

## How It Sits on the Open Drive

Drive Pro is installed beside the open Drive, in the same host and the same database, and takes its place where the two meet:

* **One file table.** A file is the open Drive's file descriptor; Drive Pro adds its fields to it - the directory a file is in and, in a personal drive, its owner - as [object extensions](https://abp.io/docs/latest/framework/fundamentals/object-extensions). A file attached through the open file API is the same file the library shows. The directories are Drive Pro's own table.
* **The same names.** The routes stay under `api/drive`, the remote service is `Drive`, the permissions are in the `Drive` group, and the database objects have the `Drive` prefix and use the `Drive` connection string.
* **The open services, replaced.** Drive Pro replaces the implementations of the open `IDriveFileAppService` and `IDriveContainerAppService`, so the open file API keeps its routes and contracts and applies Drive Pro's [permissions](permissions.md). Listing, directories, renaming and moving are on Drive Pro's own services.
* **The open file input, replaced.** In Angular, Drive Pro registers its file input in place of the open one, so every `drive-file-input` picks from the library with no change to the forms that use it ([Angular UI](angular-ui.md)).
* **The same MCP module.** Drive Pro's tools join the open `drive_*` tools in the `drive` module ([MCP Tools](mcp.md)).

What the open Drive does is unchanged and documented there: the [BLOB storing provider](../drive/provider.md) and the system containers, [tickets](../drive/tickets.md), [hosting modes](../drive/hosting.md), Drive containers and their presets, the tenant quota, public reads.

**One rule is stricter than the open Drive's:** reading a file by its id needs permission to view it ([Permissions](permissions.md#reading-a-file-by-its-id)). In the open Drive every signed-in user of the tenant may read a user-drive file whose id they have.

## Packages

Each package depends on its open counterpart, which comes with it.

| Package | Description |
|---|---|
| `Dignite.Drive.Pro.Domain.Shared`, `.Domain` | Directories, the library's fields on the open file descriptor, personal drives, the directory import. |
| `Dignite.Drive.Pro.Application.Contracts`, `.Application` | The library and directory services, the replaced open services, the permissions and their authorization. |
| `Dignite.Drive.Pro.HttpApi`, `.HttpApi.Client` | The library's HTTP API, and its C# client proxies. |
| `Dignite.Drive.Pro.EntityFrameworkCore`, `.MongoDB` | The database providers. |
| `Dignite.Drive.Pro.Mcp` | The library's [MCP tools](mcp.md). |
| `@dignite-projects/ng.drive.pro` | [Angular UI](angular-ui.md): the Drive page, the file picker, the file input. |

Drive Pro's packages are published to GitHub Packages, NuGet and npm, at the same version as the open Drive's `MAJOR.MINOR`: Drive Pro `10.7.x` is built on ABP 10.7 and works with the open Drive `10.7.y` at or above the version it names as its minimum.

## Pages

* [Setting Up Drive Pro](setting-up.md): the packages in a host, the database, what changes for the open Drive's callers.
* [The File Library](file-library.md): directories, listing, uploading into a directory, renaming and moving, the HTTP API.
* [Personal Drives](personal-drives.md): a tree for each user, and declaring one.
* [Permissions](permissions.md): the module-wide permissions, the per-item grants, and who may do what.
* [Angular UI](angular-ui.md): the Drive page, the file picker and the file input.
* [MCP Tools](mcp.md): browsing the drives and filing uploads from an AI assistant.
* [Importing a Directory Tree](importing-directories.md): filing existing files into directories by a path they carry.

## See Also

* [Drive](../drive/index.md)
