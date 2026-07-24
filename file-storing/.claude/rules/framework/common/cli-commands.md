---
paths:
  - "**/*.csproj"
  - "Directory.Packages.props"
  - "Directory.Build.props"
---

# Build, Test & Package Commands

> **ABP CLI docs**: https://abp.io/docs/latest/cli — day to day this is plain `dotnet` plus this repo's
> central package management; the ABP CLI matters mainly for regenerating the Angular proxy against the
> demo `host/`.

## Build / test

```bash
# One solution aggregates both module trees (core/ + file-explorer/)
dotnet build Dignite.Abp.FileStoring.slnx
dotnet test Dignite.Abp.FileStoring.slnx

# A single test project (e.g. iterate on Core without starting the embedded mongod that the
# MongoDB provider tests need)
dotnet test core/test/Dignite.Abp.FileStoring.Tests
dotnet test file-explorer/test/Dignite.FileExplorer.EntityFrameworkCore.Tests
```

`dotnet test` on the solution starts an **embedded mongod** (MongoSandbox) for the MongoDB provider tests —
no local MongoDB install needed. EF Core tests run against in-memory Sqlite. See `framework/testing/patterns.md`.

## Angular library (this repo *does* have a frontend)

```bash
cd angular
npm install --legacy-peer-deps
npm run build:lib        # ng build file-explorer  → the publishable library
npm start                # ng serve                → demo app on http://localhost:4200
```

The Angular library under `angular/projects/file-explorer` ships an **ABP-generated** proxy
(`abp generate-proxy -t ng`) plus components. When a `*.Application.Contracts` signature changes, regenerate
the proxy against the running `host/` rather than hand-editing it — proxy/contract drift was a real audit
finding. Same for the C# proxies in `HttpApi.Client`.

## Running the demo host

```bash
dotnet run --project host/Dignite.FileExplorer.Web.Host     # https://localhost:44390
```

`host/` is a **local-dev-only** ABP MVC host (app-nolayers), never packed/published. It opts out of central
package management (`common.props` sets `ManagePackageVersionsCentrally=false`) and pins its own versions.

## Central package management — adding/updating a NuGet dependency

Library package versions live in `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`). To add a
dependency to a **library** project:

1. Add (or confirm) a `<PackageVersion Include="Pkg.Name" Version="x.y.z" />` line in
   `Directory.Packages.props`, grouped under the matching `<ItemGroup Label="...">`.
2. Reference it in the project's `.csproj` with **no version**: `<PackageReference Include="Pkg.Name" />`.

Never put a `Version=` on a `<PackageReference>` inside a library `.csproj` — that defeats central package
management and will drift from the pinned version. (The `host/` project is the deliberate exception; it manages
its own inline versions.)

`Directory.Packages.props` also carries **security pins** (e.g. the `SQLitePCLRaw.*` family pinned to 2.1.12
past CVE-2025-6965) with `CentralPackageTransitivePinningEnabled=true` so they override transitive resolves.
`NuGetAudit` is on — keep it that way.

## Project references between the two module trees

`file-explorer` projects reference the `core` projects directly, e.g. `FileExplorer.Domain` → the FileStoring
core:

```xml
<ProjectReference Include="..\..\..\core\src\Dignite.Abp.FileStoring\Dignite.Abp.FileStoring.csproj" />
```

`abp add-package-ref` can do this and also wire the module `[DependsOn(...)]`:

```bash
abp add-package-ref Dignite.Abp.FileStoring -t file-explorer/src/Dignite.FileExplorer.Domain/Dignite.FileExplorer.Domain.csproj
```

## Packaging (NuGet)

Version, license, and metadata come from the root `Directory.Build.props` (`<Version>`,
`PackageLicenseExpression` = `LGPL-3.0-only`, `PackageProjectUrl`). Build local packages for testing:

```bash
# Packs every library project (core + file-explorer). Non-packable projects (tests, host, IsPackable=false)
# are skipped automatically.
dotnet pack Dignite.Abp.FileStoring.slnx -c Release
```

Bump `<Version>` in `Directory.Build.props` before a real release — it applies to every library project. Keep
the Angular package version (`angular/projects/file-explorer/package.json`) in step for coordinated releases.
See `framework/common/versioning.md` for what the MAJOR/MINOR/PATCH segments mean (MAJOR tracks the ABP major
version) before bumping.

## Not applicable in this repo

- `abp suite generate` — no `.suite/entities/` here.
- A module-level `DbMigrator` — the library projects ship **no** migrations; the demo `host/` owns its own
  (see `framework/data/ef-core.md`).
