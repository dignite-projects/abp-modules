# Dignite.Abp.FileStoring

An extensible file-upload framework for **ABP Framework** (LGPL-3.0) layered on **ABP BlobStoring**, plus an
optional DDD **File Explorer** backend (directory tree + persisted file metadata + REST API) and an **Angular
UI** library. **The packages this repo distributes are class libraries only** — `core/src/` (the FileStoring
core + optional `Imaging`) + `file-explorer/src/` + the Angular library under `angular/projects/`. The repo also
carries **local-dev-only demo apps that are never packed/published** — `host/` (a runnable ABP MVC host
scaffolded by ABP Studio) and `angular/`'s demo app — that exist solely to run/demo the stack end-to-end. A real
consuming application brings its own host; `host/` and `angular/` are this repo's own smoke test / live
documentation, not that host. The code was **extracted from `dignite-abp`** (treated as a frozen source); the
repo is at `10.0.0-rc.1`; its audit remediation backlog is tracked in the closed `audit`-labeled issues (#2–#35).

## Tech stack

- **.NET 10** (SDK pinned in `global.json`), **ABP Framework 10.5.0**.
- **Central package management** (`Directory.Packages.props`) — every library package version is pinned there;
  a library `.csproj` only ever has `<PackageReference Include="..." />` with no `Version=`. The demo `host/`
  opts out (`common.props`) and pins inline.
- **Single-targeted `net10.0`** across the board — including the contract layers (`Domain.Shared`,
  `Application.Contracts`, `HttpApi.Client`). No `netstandard` multi-targeting.
- Persistence: EF Core and MongoDB, both implementing the same custom repository interfaces
  (`IFileDescriptorRepository`, `IDirectoryDescriptorRepository`).
- Object mapping: **Mapperly** (compile-time; moved off AutoMapper deliberately).
- Tests: xUnit + Shouldly + NSubstitute + `Volo.Abp.TestBase` (Autofac); EF Core tests run against in-memory
  Sqlite, MongoDB tests against an embedded mongod (MongoSandbox). The repository scenarios are written once as
  abstract classes in a shared `TestBase` project and run against both providers — see
  `.claude/rules/framework/testing/patterns.md`.
- License: LGPL-3.0-only.

## Solution layout

One `.slnx` solution — **`Dignite.Abp.FileStoring.slnx`** — aggregates both module trees + the demo host:

- **`core/`** — extends ABP BlobStoring, no DDD layers:
  `core/src/{Dignite.Abp.FileStoring, Dignite.Abp.FileStoring.Imaging}` + `core/test`. The core adds an
  **`IFileHandler` upload pipeline** to blob containers (`FileSizeLimitHandler`, `FileTypeCheckHandler`, and —
  in `Imaging` — `ImageResizeHandler`), configured per container via `BlobContainerConfigurationExtensions`.
- **`file-explorer/`** — a DDD application built on the core:
  `file-explorer/src/{Domain.Shared, Domain, Application.Contracts, Application, HttpApi, HttpApi.Client,
  EntityFrameworkCore, MongoDB}` + `file-explorer/test`. Aggregates `FileDescriptor` / `DirectoryDescriptor`,
  domain services `FileDescriptorManager` / `DirectoryManager`, custom repositories, resource-based
  authorization, and ABP **conventional (auto) API controllers** under `/api/file-explorer`.

The `core/` FileStoring packages are **independently usable** without `file-explorer` (mode 1 below); that
boundary is enforced by project references (the core never references `file-explorer`), not by the solution
file. The single solution is a build/dev convenience.

Two **local-dev-only** sibling folders run/demo the stack, **never packed/published**. `host/` **is** in the
`.slnx` (ABP module-template convention; a Web SDK project so `dotnet pack` skips it, and it keeps its own
isolated non-central package management inside the project folder). `angular/` is npm-only and stays out of the
`.slnx`:

- **`host/`** — `Dignite.FileExplorer.Web.Host` (app-nolayers + MVC + OpenIddict + LeptonXLite). Run it with
  `dotnet run --project host/Dignite.FileExplorer.Web.Host` (→ `https://localhost:44390`). Has its own
  `Migrations/`. (End-to-end host wiring is part of the outstanding audit backlog.)
- **`angular/`** — an Angular workspace with the publishable `angular/projects/file-explorer` library (an
  **ABP-generated** proxy via `abp generate-proxy -t ng`, plus components) and a demo app that consumes it
  against `host/`'s API. npm, not MSBuild — not in the `.slnx`; demo app on `http://localhost:4200`.

`host/` and `angular/` sit above both module trees purely for local running/demoing and must never be referenced
from `core/` or `file-explorer/src/`.

Source files live at `<Project>/<mirrored namespace path>/File.cs` (every `.csproj` sets `<RootNamespace />`
empty) — not a generic `Entities/`/`Services/` split.

## Coding rules

Detailed conventions live in `.claude/rules/` and load automatically:

- `framework/common/abp-core.md`, `framework/common/file-storing-invariants.md`,
  `framework/common/versioning.md`, and `template/app.md` are **always loaded** (core ABP conventions + this
  repo's hard architectural invariants + the full solution map / "add a feature" flow + the versioning scheme).
- Everything else is **path-scoped** via `paths:` frontmatter — e.g. DDD patterns for `*.Domain/**/*.cs`, EF
  Core for `*DbContext*.cs`, tests for `test/**`.

Read `.claude/rules/template/app.md` first for the layer map, the `IFileHandler` pipeline, and the "add a
feature" flow, then `.claude/rules/framework/common/file-storing-invariants.md` before touching the upload
pipeline, blob/DB writes, directory moves, authorization, or a service's DI lifetime — those invariants encode
the exact bugs the #45–#70 hardening pass fixed (and the ones the audit is still driving toward).

## Commands

```bash
# Build / test everything (core + file-explorer) from the one solution
dotnet build Dignite.Abp.FileStoring.slnx
dotnet test Dignite.Abp.FileStoring.slnx

# `dotnet test` on the solution starts an embedded mongod for the MongoDB provider tests. To iterate
# on Core alone without that, target the core test project directly:
dotnet test core/test/Dignite.Abp.FileStoring.Tests

# Pack for local testing (version/license come from Directory.Build.props)
dotnet pack Dignite.Abp.FileStoring.slnx -c Release

# Angular library + demo
cd angular && npm install --legacy-peer-deps && npm run build:lib && npm start   # http://localhost:4200

# Run the demo host
dotnet run --project host/Dignite.FileExplorer.Web.Host                          # https://localhost:44390
```

The library projects ship **no migrations** — a consuming host owns its own DbContext/migrations (the demo
`host/` has its own `Migrations/`). Tests run against in-memory Sqlite + embedded mongod, so `dotnet test` needs
no migration step or local database install.

## Core conventions (see rules for the full picture)

- Respect ABP DDD layer boundaries: no `DbContext` in Application, DTOs at boundaries.
- Aggregates: `FileDescriptor` (`AggregateRoot<Guid>` + audit interfaces) and `DirectoryDescriptor`
  (`AuditedAggregateRoot<Guid>`), both `IMultiTenant`, with protected setters + behavior methods. Queries go
  through **custom** repositories (`IFileDescriptorRepository`, `IDirectoryDescriptorRepository`) implemented in
  **both** EF Core and MongoDB — add a query to the interface and to both providers.
- The `IFileHandler` pipeline runs on the upload stream **before** the blob is stored; size/image limits must
  bind before the stream is fully buffered, and never trust the client MIME type — see `file-storing-invariants.md`.
- Blob names are unique per `(TenantId, ContainerName)`; MD5/content dedup is race-safe via the DB unique
  constraint; a referenced blob (`ReferBlobName`) can't be deleted out from under its referrers.
- Blob and DB must not drift — order writes and compensate on failure (there is no outbox here by design).
- Authorization is two-layered: `FileExplorerPermissions` + resource-based handlers with per-container permission
  config and a pluggable `IFileDescriptorEntityAuthorizationHandler`. No bypass via temp-entity ownership;
  authorize every resource in a batch.
- Directory moves may not create cycles; validate the parent; block non-empty deletion — enforced by
  `DirectoryManager`.
- Object mapping is Mapperly (`FileExplorerApplicationMappers`) — don't reintroduce AutoMapper.
- New package version pins go in `Directory.Packages.props`, never inline in a library `.csproj`.
- Core (`Dignite.Abp.FileStoring`) must keep working **without** `file-explorer` installed.

## Design rationale

Usage and the architecture overview live in the root `README.md`. The "why" behind the hard invariants lives
inline in `.claude/rules/framework/common/file-storing-invariants.md`; the residual known limitations are noted
inline in the code (the concurrent-dedup and orphan-blob comments in `FileDescriptorManager`), and the audit
remediation is tracked in the closed `audit`-labeled issues (#2–#35). Many invariants encode bugs the #45–#70
fix pass already resolved — don't reintroduce them.

<!-- .claude/rules/ adapted from ../abp-notifications — that repo's notification/notifier/distributed-event
     rules were rewritten for this repo's BlobStoring + IFileHandler + file-explorer architecture, and its
     "removed over-engineering" invariants were replaced with this repo's own (upload pipeline, blob/DB
     consistency, authorization, directory-tree integrity) derived from the code, the #45–#70 fixes, and
     the `audit`-labeled issues #2–#35. -->
