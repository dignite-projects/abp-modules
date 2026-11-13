# Dignite.Abp.BlobManagement

Blob Management for ABP Framework (LGPL-3.0-only): the tenant's BLOB containers, managed. An **infrastructure module,
not an application**: a BLOB storing provider that applications use to keep their bytes in it, and - for the people in
front of those applications - files and directories in the same containers, with permissions on a container and on a
directory, a management page, an explorer and a file input. Hostable in an application's own process or once as its
own service. It is not a cloud drive: no personal drives, no sharing a file with someone (ABP's File Management does
that; this module does not compete with it).

The design is in [`dev-docs/blob-management-decision.md`](dev-docs/blob-management-decision.md) (the redesign of
2026-11-12: one edition, one kind of container, authorization by place). What it kept of the storage tier was decided
in [`dev-docs/drive-decision.md`](dev-docs/drive-decision.md), which is history and keeps the names the module had
then (`Dignite.Drive.*`, Drive Pro); the plan it followed is
[issue #379](https://github.com/dignite-projects/abp-modules/issues/379).

**What is built**

- Storage: `FileDescriptor`, `FileDescriptorManager` (detect, hash, dedup, save, compensate) and
  `UseBlobManagementContainer`, with EF Core and MongoDB; the tenant allocation (`BlobManagementFeatures`,
  `BlobManagementUsageManager`); the container presets (`Use…Preset`; the images preset stores a JPEG as WebP,
  upright), containers declared in the `BlobManagement` configuration section (`BlobManagementContainerSettings`,
  presets by name from `BlobManagementPresetOptions`) and the startup checks (`BlobManagementContainerValidator`);
  `IBlobManagementBlobService` with its in-process implementation, and the provider (`BlobManagementBlobProvider`,
  `UseBlobManagement`); the BLOB API (`BlobManagementBlobController`, `BlobManagementBlobAppService`,
  `IBlobManagementClientAuthorizer`) and its C# client proxies (`RemoteBlobManagementBlobService`); public reads with
  sizing (`BlobManagementPublicController`, `BlobManagementPublicAppService`, `IBlobManagementPublicUrlBuilder`);
  tickets (`BlobManagementTicketController`, `BlobManagementTicketRedemptionController`,
  `BlobManagementTicketProtector`, `IBlobManagementTicketClient`).
- Files and directories for people: `DirectoryDescriptor` and `DirectoryManager` (in a container that sets
  `AllowDirectories`), uploaded files addressed by id with a `DirectoryId`; `IBlobManagementContainerAppService`,
  `IBlobManagementFileAppService` (list, one file, bytes, upload, upload link, rename, move, delete),
  `IBlobManagementDirectoryAppService` (list a level, one, create, rename, move, delete, restrict) under
  `api/blob-management/containers`, `/files`, `/directories`; the authorization by place
  (`BlobManagementAccessService`); `DirectoryPathImporter` for an application that migrates its own file library in.
- `Dignite.Abp.BlobManagement.Mcp`: the `blob_*` tools. `@dignite/ng.blob-management`: the management page
  (`blob-management-page`, routed by `createRoutes()` behind `BlobManagement.Containers`), `blob-explorer`,
  `blob-file-picker`, `blob-file-input`, the proxies, and `provideBlobManagementConfig()` in the `/config` entry point.
- The texts are in 25 cultures; the user documentation is in `docs/en/blob-management/`.

Nothing is published: the packages are not packable by default (`Directory.Build.props`, until the owner releases the
module), so no release ships them, and CI passes `-p:PackBlobManagement=true` to keep verifying how they pack.

## Structure

One `.slnx` — `Dignite.Abp.BlobManagement.slnx`:

- **`src/`**

  | Project | Responsibility | Depends on |
  |---|---|---|
  | `Dignite.Abp.BlobStoring.BlobManagement` | The `IBlobProvider`: `BlobManagementBlobProvider`, `UseBlobManagement(...)`, `IBlobManagementPublicUrlBuilder`, `IBlobManagementTicketClient` | `Volo.Abp.BlobStoring`, `Application.Contracts` |
  | `Dignite.Abp.BlobManagement.Domain.Shared` | Constants, error codes, resource names, the file type table, localization | ABP |
  | `Dignite.Abp.BlobManagement.Domain` | `FileDescriptor` and `DirectoryDescriptor`, their managers and repositories, the container configuration, presets, features, usage, the directory importer | `Domain.Shared`, `blob-storing/` (`Dignite.Abp.BlobStoring.Pipeline`, `.Imaging`, `.Imaging.SkiaSharp` - so SkiaSharp is a dependency of the module: the images preset stores a JPEG as WebP) |
  | `Dignite.Abp.BlobManagement.Application.Contracts` | `IBlobManagementBlobService` (what the provider calls), the container, file and directory service interfaces, DTOs, permission names | `Domain.Shared` |
  | `Dignite.Abp.BlobManagement.Application` | The in-process `IBlobManagementBlobService`, the application services and their authorization, tickets, public reads | `Domain`, `Application.Contracts`, `Volo.Abp.PermissionManagement.Domain` (to remove a deleted directory's grants) |
  | `Dignite.Abp.BlobManagement.HttpApi` / `.HttpApi.Client` | BLOB API, container, file and directory APIs, tickets, public read / static proxies, incl. the remote `IBlobManagementBlobService` | `Application.Contracts` |
  | `Dignite.Abp.BlobManagement.EntityFrameworkCore` / `.MongoDB` | `BlobManagementDbProperties.DbTablePrefix = "Blob"` (`BlobFileDescriptors`, `BlobDirectoryDescriptors`), `ConnectionStringName = "BlobManagement"`, `ConfigureBlobManagement()` | `Domain` |
  | `Dignite.Abp.BlobManagement.Mcp` | The `blob_*` tools over the container, file and directory services | `Application.Contracts`, `aspnetcore-mcp/` |
  | `Dignite.Abp.BlobManagement.Installer` | ABP Studio / Suite install entry point, embeds `Dignite.Abp.BlobManagement.abpmdl` | `Volo.Abp.VirtualFileSystem` |

- **`test/`** — `TestBase` (`IsTestProject=false`, shared base classes), `Domain.Tests` / `Application.Tests`
  (provider-agnostic scenarios, abstract), `EntityFrameworkCore.Tests` (SQLite) and `MongoDB.Tests` (embedded mongod),
  which run those scenarios by project reference, `HttpApi.Tests` (a TestServer host that runs the module as a service
  and calls it over HTTP through the C# client proxies) and `Mcp.Tests` (the `blob_*` tools against substituted
  services). The last four are the ones `dotnet test` and CI run.
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

1. **Two ways in, one table.** An application saves a BLOB under a name (the provider, `api/blob-management/blobs/...`,
   client tickets): the name is its address, unique among the container's live files. A person uploads a file (the file
   API, the explorer, the file input, a user's upload link): it is addressed by its id and its name may repeat. Both are
   `FileDescriptor` rows of any managed container; the difference is the file's (`IsAddressedByName`), not the
   container's. There is one kind of container - `Kind` does not come back.
2. **No bridging to business permission names, and no user authorization on the BLOB path.** An application's rules run
   in the application, before the provider. The BLOB API authorizes *clients* by the container's `AllowedClientIds` and
   rejects any token that carries a user id; people are authorized by the module's own permissions.
3. **Authorization is by place, never by item.** A place is the top of a container or a directory; its scope is the
   nearest restricted directory at or above it, else the container. Every row of a listing shares the place's answer:
   a listing asks the grant store once for the place and once per restricted directory of that level, never once per
   file (`A_Listing_Should_Ask_The_Grant_Store_For_The_Place_Not_For_Each_Item`). No grants on a file, no owners, no
   creator's rights. ABP's resource permissions answer one question about one resource, by design - do not build
   anything that needs them in bulk.
4. **Authorization lives in application services only** (`[Authorize]` on the class, `BlobManagementAccessService` in
   methods); domain services never check.
5. **Directories exist only where the container allows them** (`AllowDirectories`, default `false`): every directory
   operation elsewhere fails with `BlobManagement:DirectoriesNotAllowed`, and a file there has no directory. A file
   addressed by name is never renamed or moved (`BlobManagement:FileAddressedByName`).
6. **Every BLOB has a `FileDescriptor`.** Dedup is descriptors sharing one physical BLOB, which is removed with its
   last descriptor. Tenant usage is computed from this table, **per tenant only**. A file's public address is its name
   when it is addressed by name, else its physical BLOB name, so renaming or moving never breaks a published link.
7. **One installation per deployment environment.** Never two business hosts each installing the module behind one
   gateway.
8. **Business code depends on the provider package** (plus `Application.Contracts` if it uses the file API); the host
   picks `Dignite.Abp.BlobManagement.Application` (in-process) or `Dignite.Abp.BlobManagement.HttpApi.Client` (remote).
9. **The dependency edge is `blob-management/` to `blob-storing/`, one way.** `blob-storing/` never learns of this
   module and its "what stays out" (no upload entry point, metadata, API, UI or permissions) is unchanged.
   `Dignite.Abp.BlobManagement.Mcp` may reference `aspnetcore-mcp/`, never the reverse. See root `CLAUDE.md`,
   invariant 2.
10. **It stays a BLOB management module.** Personal drives, sharing a file with a user or by link, a trash - what makes
    a cloud drive - are not added here; an application that needs them has ABP's File Management.
11. **Names are contracts once published** (nothing is yet): the package IDs, `@dignite/ng.blob-management`, the
    permission and feature group `BlobManagement`, the permission names, the remote service name, `api/blob-management`,
    the `blob_` MCP prefix, the `Blob` table prefix and the `BlobManagement` connection string name, the configuration
    section `BlobManagement`, and the entity class names (a resource permission's resource name is a type's full name:
    `Dignite.Abp.BlobManagement.BlobManagementContainer`, `Dignite.Abp.BlobManagement.DirectoryDescriptor`).
