# Dignite.Drive

A per-tenant cloud drive for ABP Framework (LGPL-3.0-only): a BLOB storing provider that applications use to keep their
bytes in the Drive, and a user-facing file library on the same storage, hostable in an application's own process or once
as its own service. The plan and the phases are in
[issue #379](https://github.com/dignite-projects/abp-modules/issues/379); the decisions and their reasons are in
[`dev-docs/drive-decision.md`](dev-docs/drive-decision.md).

**State: Phase 0.** Every project below exists as a skeleton: the ABP-generated module code with its `Sample*`
placeholders (the first real feature replaces them), and empty modules for the provider and `Dignite.Drive.Mcp`.
Nothing is published: the packages are not packable by default (`Directory.Build.props`, until the owner releases Drive),
so no release ships them, and CI passes `-p:PackDrive=true` to keep verifying how they pack. The "Responsibility"
column describes the design, most of which arrives in Phases 1-2.

## Structure

One `.slnx` — `Dignite.Drive.slnx`:

- **`src/`**

  | Project | Responsibility | Depends on |
  |---|---|---|
  | `Dignite.Abp.BlobStoring.Drive` | The `IBlobProvider`: `DriveBlobProvider`, `UseDrive(...)`, `IDrivePublicUrlBuilder`, `IDriveTicketClient` | `Volo.Abp.BlobStoring`, `Application.Contracts` |
  | `Dignite.Drive.Domain.Shared` | Constants, error codes, the file type table, localization | ABP |
  | `Dignite.Drive.Domain` | `FileDescriptor`, `DirectoryDescriptor`, their managers and repositories, container kinds, features, usage | `Domain.Shared` (later `blob-storing/`) |
  | `Dignite.Drive.Application.Contracts` | `IDriveBlobService` (what the provider calls), file/directory/container service interfaces, DTOs, permission names | `Domain.Shared` |
  | `Dignite.Drive.Application` | The in-process `IDriveBlobService`, application services, resource authorization, tickets | `Domain`, `Application.Contracts` |
  | `Dignite.Drive.HttpApi` / `.HttpApi.Client` | Blob API, file API, tickets, public read / static proxies, incl. the remote `IDriveBlobService` | `Application.Contracts` |
  | `Dignite.Drive.EntityFrameworkCore` / `.MongoDB` | `DriveDbProperties.DbTablePrefix = "Drive"`, `ConnectionStringName = "Drive"`, `ConfigureDrive()` | `Domain` |
  | `Dignite.Drive.Mcp` | `drive_*` tools over the file API | `Application.Contracts`, `aspnetcore-mcp/` |
  | `Dignite.Drive.Installer` | ABP Studio / Suite install entry point, embeds `Dignite.Drive.abpmdl` | `Volo.Abp.VirtualFileSystem` |

- **`test/`** — `TestBase` (`IsTestProject=false`, shared base classes), `Domain.Tests` / `Application.Tests`
  (provider-agnostic scenarios), `EntityFrameworkCore.Tests` (SQLite) and `MongoDB.Tests` (embedded mongod). The last
  two are the ones `dotnet test` and CI run; the scenarios reach them by project reference.
- **`host/`** — demo host (`Dignite.Drive.Host`), local development only, never packed:
  `dotnet run --project host/Dignite.Drive.Host`. Its own `Directory.Build.props` / `Directory.Packages.props` opt it out
  of the repo-root central package management. It does not reference the Drive projects yet.
- **`angular/`** — one workspace that is both the Angular host app (`src/`) and the publishable `@dignite/ng.drive`
  library (`projects/drive`); npm only, not in the `.slnx`. `projects/dev-app` is the *source project* for generating the
  library's proxies: `abp generate-proxy -t ng -m drive --source dev-app --target drive` (Host's root namespace is
  `Dignite.Drive.Host`, which would leave the Drive's types under an extra `dignite/drive` folder). Keep `Host` the first
  application in `angular.json`; the ABP Studio run profile generates the Host's own proxies without `--source`.

Namespace-mirrored files: `<Project>/<namespace path>/File.cs`, `<RootNamespace/>` empty (the test projects keep the
ABP layout). Package versions come from the root `Directory.Packages.props`; `drive/Directory.Build.props` imports the
root one, so the lockstep `<Version>` applies.

## What a change must not break

1. **Two entry points, two container kinds, kept apart.** The integration entry (the provider, `api/drive/blobs/...`)
   knows callers and tenants, never users, and works on *system* containers. The interaction entry (`api/drive/files`,
   `/directories`, `/containers`) authenticates users against the Drive's own permissions and works on *user drives*.
   Merging them brings back bridging to business permission names, which the design exists to avoid.
2. **No bridging to business permission names, and no user authorization on the blob path.** An application's rules run
   in the application, before the provider; "whoever may create content may upload" is role administration. The blob
   API authorizes *clients* by an allowed-client list on each system container, and rejects any token that carries a
   user id.
3. **One descriptor table for both roles.** Every blob has a `FileDescriptor`; dedup is descriptors sharing one physical
   blob, which is removed with its last descriptor. Tenant usage is computed from this table, **per tenant only**.
4. **Authorization lives in application services only** (`[Authorize]` on the class, resource checks in methods); domain
   services never check.
5. **One Drive installation per deployment environment.** Never two business hosts each installing it behind one gateway.
6. **Business code depends on the provider package** (plus `Application.Contracts` if it uses the file API); the host
   picks `Dignite.Drive.Application` (in-process) or `Dignite.Drive.HttpApi.Client` (remote).
7. **The dependency edge is `drive/` to `blob-storing/`, one way.** `blob-storing/` never learns of the Drive and its
   "what stays out" (no upload entry point, metadata, API, UI or permissions) is unchanged. `Dignite.Drive.Mcp` may
   reference `aspnetcore-mcp/`, never the reverse. See root `CLAUDE.md`, invariant 2.
8. **Names are contracts once published**: the package IDs, `@dignite/ng.drive`, the permission group `Drive`, the
   remote service name, `api/drive`, the `drive_` MCP prefix, the `Drive` table prefix, and the entity class names (a
   resource permission's resource name is the entity's full name). The list is in `dev-docs/drive-decision.md`.
