# Dignite.Drive

The open edition of Dignite Drive, a per-tenant cloud drive for ABP Framework (LGPL-3.0-only): a BLOB storing provider
that applications use to keep their bytes in the Drive, and what people need to attach a file - upload one, read it,
delete it - on the same storage, hostable in an application's own process or once as its own service. The file library
(listing, directories, rename, move, per-item grants, the Drive page) is **Drive Pro**, a commercial product in a
repository of its own that builds on this one. The plan and the phases are in
[issue #379](https://github.com/dignite-projects/abp-modules/issues/379); the decisions and their reasons, including the
split and which operation is on which side, are in [`dev-docs/drive-decision.md`](dev-docs/drive-decision.md).

**State: Phase 1 (the storage tier) done, and the open edition cut from Phase 2 (decided 2026-11-11).** Phase 2's file
library and Phase 3 (trash, share links) are Drive Pro's.
- Storage tier: `FileDescriptor`, `FileDescriptorManager` (detect, hash, dedup, save, compensate) and
  `UseDriveContainer`, with EF Core and MongoDB; the tenant allocation (`DriveFeatures`, `DriveUsageManager`); the
  container presets (`Use…Preset`; the images preset stores a JPEG as WebP, upright), containers declared in the
  `Drive` configuration section (`DriveContainerSettings`, presets by name from `DrivePresetOptions`) and the startup
  checks (`DriveContainerValidator`); `IDriveBlobService`
  with its in-process implementation, and the provider (`DriveBlobProvider`, `UseDrive`); the BLOB API
  (`DriveBlobController`, `DriveBlobAppService`, `IDriveClientAuthorizer`) and its C# client proxies
  (`RemoteDriveBlobService`); public reads with sizing (`DrivePublicController`, `DrivePublicAppService`,
  `IDrivePublicUrlBuilder`); tickets (`DriveTicketController`, `DriveTicketRedemptionController`, `DriveTicketProtector`,
  `IDriveTicketClient`).
- Attaching files: user drives (`DriveContainerKind.UserDrive`, files addressed by id); the file API
  (`IDriveFileAppService`: upload, upload link, details, bytes, delete; `IDriveContainerAppService`: the user drives
  with their limits; `api/drive/files`, `/containers`); the open rule (`DrivePermissions`: `Drive.Files.Upload`,
  `Drive.Files.Delete`, the uploader deletes their own file, any signed-in user reads by id); user upload tickets
  (`FileUpload`, `api/drive/tickets/upload-file`); the five open `drive_*` MCP tools (`Dignite.Drive.Mcp`);
  `@dignite/ng.drive` (`drive-file-input` under `Drive.FileInputComponent`, the proxies, `DriveFileUrlService`,
  `ObjectUrlService`, the `driveFileSize` pipe).
- The user documentation is in `docs/en/drive/`.

Nothing is published: the packages are not packable by default (`Directory.Build.props`, until the owner releases Drive),
so no release ships them, and CI passes `-p:PackDrive=true` to keep verifying how they pack.

## Structure

One `.slnx` — `Dignite.Drive.slnx`:

- **`src/`**

  | Project | Responsibility | Depends on |
  |---|---|---|
  | `Dignite.Abp.BlobStoring.Drive` | The `IBlobProvider`: `DriveBlobProvider`, `UseDrive(...)`, `IDrivePublicUrlBuilder`, `IDriveTicketClient` | `Volo.Abp.BlobStoring`, `Application.Contracts` |
  | `Dignite.Drive.Domain.Shared` | Constants, error codes, the file type table, localization | ABP |
  | `Dignite.Drive.Domain` | `FileDescriptor`, its manager and repository, container kinds and configuration, presets, features, usage | `Domain.Shared`, `blob-storing/` (`Dignite.Abp.BlobStoring.Pipeline`, `.Imaging`, `.Imaging.SkiaSharp` - so SkiaSharp is a dependency of the Drive: the images preset stores a JPEG as WebP) |
  | `Dignite.Drive.Application.Contracts` | `IDriveBlobService` (what the provider calls), the file and container service interfaces (the seams Drive Pro replaces), extensible DTOs, permission names | `Domain.Shared` |
  | `Dignite.Drive.Application` | The in-process `IDriveBlobService`, application services with the open rule, tickets, public reads | `Domain`, `Application.Contracts` |
  | `Dignite.Drive.HttpApi` / `.HttpApi.Client` | Blob API, file API, tickets, public read / static proxies, incl. the remote `IDriveBlobService` | `Application.Contracts` |
  | `Dignite.Drive.EntityFrameworkCore` / `.MongoDB` | `DriveDbProperties.DbTablePrefix = "Drive"`, `ConnectionStringName = "Drive"`, `ConfigureDrive()` | `Domain` |
  | `Dignite.Drive.Mcp` | The five open `drive_*` tools over the file API | `Application.Contracts`, `aspnetcore-mcp/` |
  | `Dignite.Drive.Installer` | ABP Studio / Suite install entry point, embeds `Dignite.Drive.abpmdl` | `Volo.Abp.VirtualFileSystem` |

- **`test/`** — `TestBase` (`IsTestProject=false`, shared base classes), `Domain.Tests` / `Application.Tests`
  (provider-agnostic scenarios), `EntityFrameworkCore.Tests` (SQLite) and `MongoDB.Tests` (embedded mongod), which the
  scenarios reach by project reference, `HttpApi.Tests` (a TestServer host that runs the Drive as a service and calls
  it over HTTP through the C# client proxies) and `Mcp.Tests` (the `drive_*` tools against substituted services). The
  last four are the ones `dotnet test` and CI run.
- **`host/`** — demo host (`Dignite.Drive.Host`), local development only, never packed:
  `dotnet run --project host/Dignite.Drive.Host`. Its own `Directory.Build.props` / `Directory.Packages.props` opt it out
  of the repo-root central package management. It installs the Drive in-process (Application, HttpApi, EF Core; its
  containers are in `appsettings.json`); its migrations are in `Migrations/`. It is also where the C# client proxies come
  from: with the host running (`abp install-libs` in its folder first), run
  `abp generate-proxy -t csharp -u https://localhost:44352 -m drive --without-contracts` in
  `src/Dignite.Drive.HttpApi.Client`.
- **`angular/`** — one workspace that is both the Angular host app (`src/`) and the publishable `@dignite/ng.drive`
  library (`projects/drive`); npm only, not in the `.slnx`. The library has one entry point (`src/public-api.ts`) and
  no `@dignite/ng.drive/config`: it has no routes, menu items or pages to register, and an application imports
  `DriveFileInputComponent` where a form uses it. The demo app's home page shows the file input. `projects/dev-app` is
  the *source project* for generating the library's proxies: `abp generate-proxy -t ng -m drive --source dev-app
  --target drive` (Host's root namespace is `Dignite.Drive.Host`, which would leave the Drive's types under an extra
  `dignite/drive` folder). Keep `Host` the first application in `angular.json`; the ABP Studio run profile generates the
  Host's own proxies without `--source`.

Namespace-mirrored files: `<Project>/<namespace path>/File.cs`, `<RootNamespace/>` empty (the test projects keep the
ABP layout). Package versions come from the root `Directory.Packages.props`; `drive/Directory.Build.props` imports the
root one, so the lockstep `<Version>` applies.

## What a change must not break

1. **Two entry points, two container kinds, kept apart.** The integration entry (the provider, `api/drive/blobs/...`)
   knows callers and tenants, never users, and works on *system* containers addressed by the application's own names.
   The interaction entry (`api/drive/files`, `/containers`) authenticates users against the Drive's own permissions and
   works on *user drives*, whose files are addressed by id. Merging them brings back bridging to business permission
   names, which the design exists to avoid.
2. **No bridging to business permission names, and no user authorization on the blob path.** An application's rules run
   in the application, before the provider; "whoever may create content may upload" is role administration. The blob
   API authorizes *clients* by an allowed-client list on each system container, and rejects any token that carries a
   user id.
3. **One file table, here, for both roles and both editions.** Every blob has a `FileDescriptor`; dedup is descriptors
   sharing one physical blob, which is removed with its last descriptor. Tenant usage is computed from this table,
   **per tenant only**. Drive Pro keeps no file table of its own: it adds its per-file fields (the directory, the owner
   of a personal drive) to this row through ABP's object extensions, so `FileDescriptor` gains no library columns here,
   and a file attached through the open Drive is the same file Drive Pro's library shows.
4. **Authorization lives in application services only** (`[Authorize]` on the class, the uploader check in methods);
   domain services never check.
5. **One Drive installation per deployment environment.** Never two business hosts each installing it behind one gateway.
6. **Business code depends on the provider package** (plus `Application.Contracts` if it uses the file API); the host
   picks `Dignite.Drive.Application` (in-process) or `Dignite.Drive.HttpApi.Client` (remote).
7. **The dependency edge is `drive/` to `blob-storing/`, one way.** `blob-storing/` never learns of the Drive and its
   "what stays out" (no upload entry point, metadata, API, UI or permissions) is unchanged. `Dignite.Drive.Mcp` may
   reference `aspnetcore-mcp/`, never the reverse. See root `CLAUDE.md`, invariant 2.
8. **The open edition stays open, and Drive Pro can still take its place.** What the decision record puts on Drive Pro's
   side - listing a container's files, directories, rename, move, the Drive's per-item grants, personal drives, drive
   pages, trash, share links - is not added here, and nothing here references Drive Pro. The seams it replaces keep
   working for a replacement: `IDriveFileAppService` and `IDriveContainerAppService` (Drive Pro replaces their
   implementations, so their members stay virtual and their DTOs extensible, without library members), and the Angular
   key `Drive.FileInputComponent` with the shape of `DriveFileInputReplaceableData` (a consumer's markup is the same
   whichever component is registered). Drive Pro's MCP tools extend the `drive` module, so an open tool's name and
   parameters are not changed to take a directory.
9. **Names are contracts once published**: the package IDs, `@dignite/ng.drive`, the permission group `Drive`, the
   remote service name, `api/drive`, the `drive_` MCP prefix, the `Drive` table prefix and connection string name,
   `Drive.FileInputComponent`, and the entity class names (Drive Pro registers its object extensions on
   `FileDescriptor`; a resource permission's resource name is an entity's full name). Drive Pro adds to these names
   rather than taking new ones ("Drive Pro's names" in `dev-docs/drive-decision.md`).
