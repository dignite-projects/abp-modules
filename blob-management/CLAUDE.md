# Dignite.Abp.BlobManagement

Blob Management for ABP Framework (LGPL-3.0-only): the tenant's BLOB containers, managed. An **infrastructure module,
not an application**: a BLOB storing provider with a ledger. Business systems keep their bytes in it - as BLOBs under
a name, or as files by id, filed in directories - and the module writes a descriptor for each, from which come the
tenant's allocation, the usage figures and a page an administrator reads. It serves business systems and has no
upload of its own: a business system builds its own file UI and calls the module as a client. The one thing a user
changes here is the directories of a container, by permissions put on the container. Hostable in an application's own
process or once as its own service. It is not a cloud drive: no personal drives, no sharing a file with someone
(ABP's File Management does that; this module does not compete with it).

The design is in [`dev-docs/positioning-decision.md`](dev-docs/positioning-decision.md) (2026-11-13: what the module
is for, who calls it, usage; tickets, the user-facing file API and restricted directories removed), which narrows
[`dev-docs/blob-management-decision.md`](dev-docs/blob-management-decision.md) (the redesign of 2026-11-12: one
edition, one kind of container, the names). What they kept of the storage tier was decided in
[`dev-docs/drive-decision.md`](dev-docs/drive-decision.md), which is history and keeps the names the module had then
(`Dignite.Drive.*`, Drive Pro); the plan it followed is
[issue #379](https://github.com/dignite-projects/abp-modules/issues/379).

**What is built**

- Storage: `FileDescriptor`, `FileDescriptorManager` (detect, hash, dedup, save, compensate) and
  `UseBlobManagementContainer`, with EF Core and MongoDB; the tenant allocation (`BlobManagementFeatures`,
  `BlobManagementUsageManager`); the container presets (`Use…Preset`; the images preset stores a JPEG as WebP,
  upright), containers declared in the `BlobManagement` configuration section (`BlobManagementContainerSettings`,
  presets by name from `BlobManagementPresetOptions`) and the startup checks (`BlobManagementContainerValidator`);
  `IBlobManagementBlobService` with its in-process implementation, and the provider (`BlobManagementBlobProvider`,
  `UseBlobManagement`); the BLOB API (`BlobManagementBlobController`, `BlobManagementBlobAppService`,
  `IBlobManagementClientAuthorizer`, `BlobManagementClientChecker`) and its C# client proxies
  (`RemoteBlobManagementBlobService`); public reads with sizing (`BlobManagementPublicController`,
  `BlobManagementPublicAppService`, `IBlobManagementPublicUrlBuilder`).
- Files by id, for a business system: `IBlobManagementFileService` (list a place, one file, bytes, store, rename,
  move, delete) with its trusted in-process implementation (`BlobManagementFileService`), the file API behind it
  (`IBlobManagementFileAppService`, `api/blob-management/files`: an allowed client for everything, a user with the
  management permission for the reads) and the remote implementation (`RemoteBlobManagementFileService`).
- Directories, for users: `DirectoryDescriptor` and `DirectoryManager` (in a container that sets `AllowDirectories`),
  `IBlobManagementDirectoryAppService` (list a level, one, create, rename, move, delete) under
  `api/blob-management/directories`, authorized by the container (`BlobManagementAccessService`);
  `IBlobManagementContainerAppService` under `/containers`; `DirectoryPathImporter` for an application that migrates
  its own file library in.
- Usage: `IBlobManagementUsageAppService` under `api/blob-management/usage` - the tenant and each container (stored
  bytes), each user and each client (uploaded bytes) - on the descriptor repository's usage queries.
- `Dignite.Abp.BlobManagement.Mcp`: the `blob_*` tools, which only read. `@dignite/ng.blob-management`: the management
  page (`blob-management-page`, routed by `createRoutes()` behind `BlobManagement.Containers`: usage, then one container
  at a time - its directory tree with its operations, its files read-only), its parts (`blob-usage`, `blob-browser`,
  `blob-directory-tree`, `blob-file-list`, `blob-file-preview`), the proxies, and `provideBlobManagementConfig()` in
  the `/config` entry point.
- The texts are in 25 cultures; the user documentation is in `docs/en/blob-management/`.

Nothing is published: the packages are not packable by default (`Directory.Build.props`, until the owner releases the
module), so no release ships them, and CI passes `-p:PackBlobManagement=true` to keep verifying how they pack.

## Structure

One `.slnx` — `Dignite.Abp.BlobManagement.slnx`:

- **`src/`**

  | Project | Responsibility | Depends on |
  |---|---|---|
  | `Dignite.Abp.BlobStoring.BlobManagement` | The `IBlobProvider`: `BlobManagementBlobProvider`, `UseBlobManagement(...)`, `IBlobManagementPublicUrlBuilder` | `Volo.Abp.BlobStoring`, `Application.Contracts` |
  | `Dignite.Abp.BlobManagement.Domain.Shared` | Constants, error codes, resource names, the file type table, localization | ABP |
  | `Dignite.Abp.BlobManagement.Domain` | `FileDescriptor` and `DirectoryDescriptor`, their managers and repositories, the container configuration, presets, features, usage, the directory importer | `Domain.Shared`, `blob-storing/` (`Dignite.Abp.BlobStoring.Pipeline`, `.Imaging`, `.Imaging.SkiaSharp` - so SkiaSharp is a dependency of the module: the images preset stores a JPEG as WebP) |
  | `Dignite.Abp.BlobManagement.Application.Contracts` | `IBlobManagementBlobService` (what the provider calls) and `IBlobManagementFileService` (what a business system's file library calls), the container, directory and usage service interfaces, DTOs, permission names | `Domain.Shared` |
  | `Dignite.Abp.BlobManagement.Application` | The in-process `IBlobManagementBlobService` and `IBlobManagementFileService`, the application services and their authorization, usage, public reads | `Domain`, `Application.Contracts`, `Volo.Abp.Users.Abstractions` (only for `IExternalUserLookupServiceProvider`, resolved optionally to name the users of the usage figures) |
  | `Dignite.Abp.BlobManagement.HttpApi` / `.HttpApi.Client` | BLOB API, file API, container, directory and usage APIs, public read / static proxies, incl. the remote `IBlobManagementBlobService` and `IBlobManagementFileService` | `Application.Contracts` |
  | `Dignite.Abp.BlobManagement.EntityFrameworkCore` / `.MongoDB` | `BlobManagementDbProperties.DbTablePrefix = "Blob"` (`BlobFileDescriptors`, `BlobDirectoryDescriptors`), `ConnectionStringName = "BlobManagement"`, `ConfigureBlobManagement()` | `Domain` |
  | `Dignite.Abp.BlobManagement.Mcp` | The `blob_*` tools over the container, file and directory services; every tool reads | `Application.Contracts`, `aspnetcore-mcp/` |
  | `Dignite.Abp.BlobManagement.Installer` | ABP Studio / Suite install entry point, embeds `Dignite.Abp.BlobManagement.abpmdl` | `Volo.Abp.VirtualFileSystem` |

- **`test/`** — `TestBase` (`IsTestProject=false`, shared base classes), `Domain.Tests` / `Application.Tests`
  (provider-agnostic scenarios, abstract), `EntityFrameworkCore.Tests` (SQLite) and `MongoDB.Tests` (embedded mongod),
  which run those scenarios by project reference, `HttpApi.Tests` (a TestServer host that runs the module as a service
  and calls it over HTTP through the C# client proxies, as a client and as a user) and `Mcp.Tests` (the `blob_*` tools
  against substituted services). The last four are the ones `dotnet test` and CI run.
- **`host/`** — demo host (`Dignite.Abp.BlobManagement.Host`), local development only, never packed:
  `dotnet run --project host/Dignite.Abp.BlobManagement.Host`. Its own `Directory.Build.props` /
  `Directory.Packages.props` opt it out of the repo-root central package management. It installs the module in-process
  (Application, HttpApi, EF Core; its containers are in `appsettings.json`); its migrations are in `Migrations/`. It is
  also where the C# client proxies come from: with the host running (`abp install-libs` in its folder first), run
  `abp generate-proxy -t csharp -u https://localhost:44352 -m blobManagement --without-contracts` in
  `src/Dignite.Abp.BlobManagement.HttpApi.Client`.
- **`angular/`** — one workspace that is both the Angular host app (`src/`) and the publishable
  `@dignite/ng.blob-management` library (`projects/blob-management`); npm only, not in the `.slnx`. The library has two
  entry points: the main one (`src/public-api.ts`: components, routes, proxies, services) and
  `@dignite/ng.blob-management/config` (`provideBlobManagementConfig()`: the menu item and route name), as ABP's own
  libraries split them. `projects/dev-app` is the *source project* for generating the library's proxies:
  `abp generate-proxy -t ng -m blobManagement --source dev-app --target blob-management` (Host's root namespace is
  `Dignite.Abp.BlobManagement.Host`, which would leave the module's types under an extra `dignite/abp/blob-management`
  folder). Keep `Host` the first application in `angular.json`; the ABP Studio run profile generates the Host's own
  proxies without `--source`.

Namespace-mirrored files: `<Project>/<namespace path>/File.cs`, `<RootNamespace/>` empty (the test projects keep the
ABP layout). Package versions come from the root `Directory.Packages.props`; `blob-management/Directory.Build.props`
imports the root one, so the lockstep `<Version>` applies.

## What a change must not break

1. **Two ways in, one table, both an application's.** It saves a BLOB under a name (the provider,
   `api/blob-management/blobs/...`): the name is its address, unique among the container's live files. Or it keeps a
   file by its id for its user (`IBlobManagementFileService`, `api/blob-management/files`): the name may repeat, and
   the file can be filed in a directory, renamed and moved. Both are `FileDescriptor` rows of any managed container;
   the difference is the file's (`IsAddressedByName`), not the container's. There is one kind of container - `Kind`
   does not come back.
2. **A file is its business system's; the module has no upload of its own.** No user stores, renames, moves or deletes
   a file through the module, whatever permission they hold: an application's rules run in the application, before it
   calls. The BLOB API and the file API authorize *clients* by the container's `AllowedClientIds` and reject any
   token that carries a user id for a write; the file API's reads also admit a user who holds the management
   permission - the management page - and nothing more. No bridging to business permission names, no tickets or
   upload links (a direct upload, if a remote deployment ever needs one, can only be built here - only the module can
   verify it), and no trusted way for an application to write directories past the permissions on them.
3. **A user's authorization is the container's, never an item's.** What a user may do with directories is the
   module-wide permission or the one granted on that container, the same for every directory in it: a listing asks
   the grant store once, for the container (`A_Listing_Should_Ask_The_Grant_Store_For_The_Container_Not_For_Each_Directory`).
   No directory has permissions of its own, no grants on a file, no owners, no creator's rights - `CreatorId` is who a
   file is counted for and gives nothing. A directory's visibility cannot be the module's to enforce: file listings
   are an application's call, which names no user. ABP's resource permissions answer one question about one
   resource, by design - do not build anything that needs them in bulk.
4. **Authorization lives in application services only** (`[Authorize]` on the class, `BlobManagementAccessService` and
   `BlobManagementClientChecker` in methods); domain services never check. The in-process `IBlobManagementBlobService`
   and `IBlobManagementFileService` are trusted and never exposed over HTTP.
5. **Directories exist only where the container allows them** (`AllowDirectories`, default `false`): every directory
   operation elsewhere fails with `BlobManagement:DirectoriesNotAllowed`, and a file there has no directory. A file
   addressed by name is never renamed or moved (`BlobManagement:FileAddressedByName`).
6. **Every BLOB has a `FileDescriptor`.** Dedup is descriptors sharing one physical BLOB, which is removed with its
   last descriptor. Usage is computed from this table, in the current tenant, in two measures that are never mixed:
   **stored bytes** (the tenant, a container: each physical BLOB once; the containers add up to the tenant, and the
   allocation is checked against it) and **uploaded bytes** (a user, a client: the sizes of their descriptors, shared
   content counted for each). The allocation is the tenant's only. A file's public address is its name when it is
   addressed by name, else its physical BLOB name, so renaming or moving never breaks a published link.
7. **One installation per deployment environment.** Never two business hosts each installing the module behind one
   gateway.
8. **Business code depends on the provider package** (plus `Application.Contracts` if it keeps files by id through
   `IBlobManagementFileService`); the host picks `Dignite.Abp.BlobManagement.Application` (in-process) or
   `Dignite.Abp.BlobManagement.HttpApi.Client` (remote). The two behave alike for a caller, down to the user a file is
   counted for: the application's current user, which the remote services send since the client's token carries none.
9. **The dependency edge is `blob-management/` to `blob-storing/`, one way.** `blob-storing/` never learns of this
   module and its "what stays out" (no upload entry point, metadata, API, UI or permissions) is unchanged.
   `Dignite.Abp.BlobManagement.Mcp` may reference `aspnetcore-mcp/`, never the reverse. See root `CLAUDE.md`,
   invariant 2.
10. **It stays a BLOB management module.** Personal drives, sharing a file with a user or by link, a trash - what makes
    a cloud drive - are not added here; an application that needs them has ABP's File Management. Neither is a file UI
    for end users: the management page reads, and its MCP tools read.
11. **Names are contracts once published** (nothing is yet): the package IDs, `@dignite/ng.blob-management`, the
    permission and feature group `BlobManagement`, the permission names, the remote service name, `api/blob-management`,
    the `blob_` MCP prefix, the `Blob` table prefix and the `BlobManagement` connection string name, the configuration
    section `BlobManagement`, and the class a resource permission is granted on (its resource name is that type's
    full name: `Dignite.Abp.BlobManagement.BlobManagementContainer`). The names were weighed again on 2026-11-13 and
    stay: not File Management (ABP's module has that name, its routes and its permission group), not entities named
    after BLOBs (a descriptor is not the BLOB - several share one), not the `Abp` table prefix (ABP reserves it for
    its core modules).
