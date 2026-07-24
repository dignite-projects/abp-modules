# Dignite ABP Modules — monorepo guide

Two independently installable **ABP Framework** module trees, developed together and released in
lockstep. This file covers what is true for the *repository*; each module carries its own
`CLAUDE.md` and `.claude/rules/` with the conventions and hard invariants that apply to its code.

**Read the module-level docs before changing code inside a module.** They are not duplicates of
each other — the two modules have genuinely different DDD shapes, persistence conventions, and
invariants, and the rules are written per module on purpose.

| Working in | Read first |
|---|---|
| `file-storing/**` | [`file-storing/CLAUDE.md`](file-storing/CLAUDE.md), then `file-storing/.claude/rules/template/app.md`, then `file-storing/.claude/rules/framework/common/file-storing-invariants.md` |
| `notifications/**` | [`notifications/CLAUDE.md`](notifications/CLAUDE.md), then `notifications/.claude/rules/template/app.md`, then `notifications/.claude/rules/framework/common/notifications-invariants.md` |

## What lives where

```
abp-modules/
├── Directory.Build.props        # shared metadata + the ONE <Version> every package uses
├── Directory.Packages.props     # central package management for all library projects
├── Dignite.Abp.Modules.slnx     # aggregate solution (both modules)
├── .github/workflows/           # one build+test workflow, one lockstep release workflow
├── file-storing/                # file upload pipeline on ABP BlobStoring + DDD File Explorer
└── notifications/               # event-driven notifications + optional Notification Center
```

Each module holds `core/`, its feature tree (`file-explorer/` or `notification-center/`), a
local-dev-only `host/`, an `angular/` workspace, its own focused `.slnx`, and its own `.claude/`.

## Repository-wide invariants

These hold across both modules and are the things most likely to be broken by an otherwise
reasonable-looking change:

1. **PackageIds and root namespaces never change.** Every package keeps the ID it has always had;
   living in a subdirectory changed nothing for consumers (PackageId follows AssemblyName, not the
   folder). Never rename a package or root namespace to "match" the new layout.
2. **`<AssemblyVersion>` is pinned at `1.0.0.0`** in the root `Directory.Build.props` and is never
   bumped alongside `<Version>`. This is load-bearing for notifications specifically — see
   `notifications/.claude/rules/framework/common/notifications-invariants.md` §1. Don't "fix" it to
   track the release version.
3. **One lockstep `<Version>` for the entire repository**, MAJOR ≥ 10 (it tracks the targeted ABP
   major and must stay permanently above the legacy `3.8.2` package lines on NuGet). There is no
   per-module or per-project version. See [CONTRIBUTING.md](CONTRIBUTING.md#versioning-and-releases).
4. **The two modules never reference each other.** `file-storing/` and `notifications/` share this
   repository for development and release only. A `ProjectReference` across that boundary is a bug.
5. **Library package versions live in the root `Directory.Packages.props`**, never inline in a
   library `.csproj`. The two demo hosts are the deliberate exception — each opts out of central
   package management and pins inline.

## Build & test

```bash
dotnet build Dignite.Abp.Modules.slnx     # both modules
dotnet test  Dignite.Abp.Modules.slnx

dotnet build file-storing/Dignite.Abp.FileStoring.slnx      # one module, smaller graph
dotnet build notifications/Dignite.Abp.Notifications.slnx
```

`dotnet test` starts an embedded mongod (MongoSandbox) for MongoDB provider tests and uses in-memory
SQLite for EF Core ones — no local database install needed. The Angular libraries are npm workspaces
under each module's `angular/`, outside MSBuild.

## Conventions that differ from plain ABP

Both modules place source at `<Project>/<mirrored namespace path>/File.cs` (every `.csproj` sets
`<RootNamespace />` empty) rather than a generic `Entities/`/`Services/` split — put a new file at
the folder path matching its namespace.

Both modules' `host/` and `angular/` folders are **local-dev demos only**, never packed or published
(`IsPackable=false`), and must never be referenced from the library projects. The dependency arrow
points from demos down into libraries, never back.
