# Dignite.Abp.FileStoring — Solution Structure

> **Docs**: https://abp.io/docs/latest/solution-templates/module-development-template

The **distributed packages** are class libraries only — two ABP module trees (`core/` and `file-explorer/`)
under a **single** `.slnx` solution. There is no *production* `Host`, `DbMigrator`, or frontend to publish; a
real consuming application (not in this repo) references these projects (or their NuGet packages) and owns the
running app. The repo *does* carry a **local-dev-only** demo `host/` (a runnable ABP MVC host — **in the
`.slnx`**, but never packed) plus an `angular/` workspace (npm-only, not in the `.slnx`), purely to run/demo the
stack end-to-end. The `core/` FileStoring packages are usable **without** `file-explorer`; that independence is
enforced by project references (the core never references `file-explorer`), not by the solution file.

## Solution structure

```
abp-file-storing/
├── Dignite.Abp.FileStoring.slnx             # one solution: core/ + file-explorer/ libs + demo host/
├── Directory.Build.props                     # shared MSBuild props (LangVersion, Nullable, Version, license)
├── Directory.Packages.props                  # ⚠️ central package management — ALL library versions live here
├── common.props                              # host-only props (opts the host OUT of central package mgmt)
├── global.json                               # .NET SDK pin (10.0.302)
├── core/                                     # extends ABP BlobStoring — NO DDD layers
│   ├── src/
│   │   ├── Dignite.Abp.FileStoring/          # upload infrastructure on ABP BlobStoring: the IFileHandler
│   │   │                                     #   pipeline (FileSizeLimitHandler, FileTypeCheckHandler),
│   │   │                                     #   ContainerNameValidator, IBlobNameGenerator/RandomBlobNameGenerator,
│   │   │                                     #   BlobContainerConfigurationExtensions, FileConsts. net10.0.
│   │   └── Dignite.Abp.FileStoring.Imaging/  # optional upload-time image processing: ImageResizeHandler
│   │                                         #   (Abp.Imaging + ImageSharp). net10.0.
│   └── test/{Dignite.Abp.FileStoring.Tests, Dignite.Abp.FileStoring.Imaging.Tests}
└── file-explorer/                            # a DDD application built ON the FileStoring core
    ├── src/
    │   ├── Dignite.FileExplorer.Domain.Shared/       # constants, FileExplorerErrorCodes, localization, settings keys
    │   ├── Dignite.FileExplorer.Domain/              # FileDescriptor / DirectoryDescriptor aggregates,
    │   │                                             #   FileDescriptorManager / DirectoryManager,
    │   │                                             #   IFileDescriptorRepository / IDirectoryDescriptorRepository,
    │   │                                             #   FileExplorerDbProperties. Depends on the FileStoring core.
    │   ├── Dignite.FileExplorer.Application.Contracts/  # DTOs, service interfaces, FileExplorerPermissions
    │   ├── Dignite.FileExplorer.Application/         # AppServices, FileExplorerApplicationMappers (Mapperly),
    │   │                                             #   resource-based authorization handlers
    │   ├── Dignite.FileExplorer.HttpApi/             # ABP conventional (auto) API controllers
    │   ├── Dignite.FileExplorer.HttpApi.Client/      # C# client proxies for remote consumers
    │   ├── Dignite.FileExplorer.EntityFrameworkCore/ # repository impl #1 (relational) + FileExplorerDbContext
    │   └── Dignite.FileExplorer.MongoDB/             # repository impl #2 (document)
    └── test/
        ├── Dignite.FileExplorer.TestBase/            # shared provider-independent base + abstract scenarios
        ├── Dignite.FileExplorer.EntityFrameworkCore.Tests/  # EF Core / in-memory Sqlite provider
        ├── Dignite.FileExplorer.MongoDB.Tests/       # MongoDB provider (embedded mongod via MongoSandbox)
        ├── Dignite.FileExplorer.Domain.Tests/        # domain manager rules
        ├── Dignite.FileExplorer.Application.Tests/   # AppService + container-authorization config
        ├── Dignite.FileExplorer.Authorization.Tests/ # resource-based authorization & permission gating
        ├── Dignite.FileExplorer.DirectorySafety.Tests/ # directory cycle prevention + localization
        └── Dignite.FileExplorer.Update.Tests/        # rename/patch semantics
```

Two **local-dev-only** sibling folders demo the stack, **never packed/published**:
- **`host/`** — `Dignite.FileExplorer.Web.Host`, a runnable ABP MVC host (app-nolayers, OpenIddict, LeptonXLite),
  scaffolded by ABP Studio. In the `.slnx` but a Web SDK project, so `dotnet pack` skips it. It opts out of
  central package management via `common.props` and pins its own versions, has its own `Migrations/`, and runs on
  `https://localhost:44390`. Its job is to run/demo FileExplorer end-to-end.
- **`angular/`** — an Angular workspace with the publishable `angular/projects/file-explorer` library (an
  **ABP-generated** proxy + file-explorer components) plus a demo app that consumes it against `host/`'s API.
  npm, not MSBuild — not in the `.slnx`; demo app on `http://localhost:4200`.

`host/` and `angular/` sit above both module trees purely for local running/demoing and must never be referenced
from `core/` or `file-explorer/src/`.

Source files live at `<Project>/<mirrored namespace path>/File.cs` (every `.csproj` sets `<RootNamespace />`
empty) — not a generic `Entities/`/`Services/` split.

## File layout convention

Source files live under `<ProjectFolder>/<mirrored namespace path>/File.cs`. E.g. the
`Dignite.Abp.FileStoring` namespace lives at
`core/src/Dignite.Abp.FileStoring/Dignite/Abp/FileStoring/*.cs`. Note `file-explorer/Domain` also carries a
`Dignite/Abp/FileStoring/` subtree (`FileCell`, `FileGridConfiguration`, `FileExplorerBlobContainerConfigurationNames`)
— the file-explorer-specific extensions to the core's blob-container configuration — alongside its own
`Dignite/FileExplorer/` subtree. Put a new file at the folder path matching its namespace; don't add a flat
`Entities/`/`Services/` subfolder. (Test projects that place `.cs` at the project root are the exception — match
whatever the project you're editing already does.)

## Layer responsibilities

| Project | Responsibility | Depends on |
|---|---|---|
| `Abp.FileStoring` (Core) | ABP BlobStoring + `IFileHandler` upload pipeline, container config, blob naming | ABP BlobStoring |
| `Abp.FileStoring.Imaging` | `ImageResizeHandler` (upload-time image processing) | Core, ABP Imaging/ImageSharp |
| `FileExplorer.Domain.Shared` | Constants, error codes, localization, settings keys | ABP Validation |
| `FileExplorer.Domain` | Aggregates, managers, custom repository interfaces | Domain.Shared, FileStoring Core |
| `FileExplorer.Application.Contracts` | DTOs, service interfaces, permissions | Domain.Shared, ABP Authorization |
| `FileExplorer.Application` | AppServices, Mapperly mapping, authorization handlers | Application.Contracts, Domain, Imaging |
| `FileExplorer.HttpApi` | Auto (conventional) API controllers | Application.Contracts |
| `FileExplorer.HttpApi.Client` | Remote C# client proxies | Application.Contracts |
| `FileExplorer.EntityFrameworkCore` / `.MongoDB` | Custom repository implementations | Domain |

## The core extension model — the `IFileHandler` pipeline

The heart of `core/` is a per-container **handler pipeline** layered on ABP BlobStoring. A handler is a plain
class:

```csharp
public interface IFileHandler
{
    Task ExecuteAsync(FileHandlerContext context); // context = FileName, MimeType, mutable BlobStream, container config
}
```

Handlers are attached to a blob container via `BlobContainerConfigurationExtensions`, which stores an ordered
`TypeList<IFileHandler>` on the container configuration:

```csharp
Configure<AbpBlobStoringOptions>(options =>
{
    options.Containers.Configure<MyPicturesContainer>(c =>
    {
        c.AddFileSizeLimitHandler(h => h.SetMaximumFileSize(2 * 1024 * 1024)); // core
        c.AddFileTypeCheckHandler(h => h.SetAllowableFileTypeNames(".png", ".jpg")); // core
        c.AddImageResizeHandler(h => /* preset */); // Imaging package
    });
});
```

At upload time `FileDescriptorManager` reads that `TypeList<IFileHandler>` from the resolved container config,
instantiates each handler, and runs `ExecuteAsync` over the stream **before** the blob is stored. A handler
inspects/validates (`FileSizeLimitHandler`, `FileTypeCheckHandler`) or **replaces** the stream
(`ImageResizeHandler` swaps `context.BlobStream` for the resized image). This is file-storing's plugin seam —
new upload rules/transforms are new handlers, added without touching `file-explorer` or consumers.

## Two operation modes — both must keep working

1. **Core FileStoring only**: install `Dignite.Abp.FileStoring` (+ optionally `.Imaging`). You get blob
   containers with the `IFileHandler` pipeline (size/type limits, image resize) — no directories, no
   `FileDescriptor` persistence, no REST API. A consuming app stores/reads blobs through ABP BlobStoring with
   this repo's handlers applied.
2. **Full FileExplorer**: also install `file-explorer` (+ `EntityFrameworkCore` or `MongoDB`). Adds the DDD
   backend — `DirectoryDescriptor` trees, persisted `FileDescriptor` metadata (dedup, MD5, associated-entity
   links), resource-based authorization, the REST API, and the Angular UI.

A change to the core must keep mode 1 working — don't make the FileStoring handlers assume `file-explorer` is
installed.

## Adding a feature

**A new upload rule / transform** (most common — no entity, no DDD layer touched):
1. Add an `IFileHandler` implementation in `core/src/Dignite.Abp.FileStoring` (or `.Imaging` for an image op),
   as a plain `ITransientDependency` class. Read/validate `context.MimeType`/`context.BlobStream`; to transform,
   replace `context.BlobStream`. Flow the request's cancellation to any I/O.
2. Add its per-handler `*Configuration` + `*ConfigurationNames`, and an `Add…Handler(...)` extension on
   `BlobContainerConfiguration` that `TryAdd<>`s it to the `TypeList<IFileHandler>` under
   `BlobContainerConfigurationNames.FileHandlers` (mirror `AddFileSizeLimitHandler`).
3. No entity/EF/Mongo change needed — the pipeline is generic.

**A new aggregate/entity in `file-explorer`** (rare — see `framework/common/development-flow.md` for the full
walkthrough):
1. Entity in `FileExplorer.Domain` (namespace-mirrored), audited aggregate root + `IMultiTenant`, protected
   setters + behavior methods.
2. Constants/error codes in `Domain.Shared`.
3. A **custom repository interface** + EF Core **and** MongoDB implementations (this repo's convention), both
   registered via `options.AddRepository<TEntity, TImpl>()`.
4. EF mapping in `ConfigureFileExplorer()` **and** the MongoDB equivalent — **no migration in the module**.
5. DTOs + service interface + permissions in `Application.Contracts`; implementation in `Application` with a
   Mapperly map and resource-based authorization.
6. Abstract cross-provider tests in `TestBase`, inherited by the EF Core + MongoDB test projects.

## Commands

```bash
dotnet build Dignite.Abp.FileStoring.slnx
dotnet test Dignite.Abp.FileStoring.slnx

# Core only (skips the embedded-mongod MongoDB provider tests):
dotnet test core/test/Dignite.Abp.FileStoring.Tests

# Angular library / demo:
cd angular && npm install --legacy-peer-deps && npm run build:lib
```

No module-level `DbMigrator` — the library projects ship no migrations; a consuming host (incl. the demo `host/`)
owns them. EF Core tests run against in-memory Sqlite and MongoDB tests against an embedded mongod (MongoSandbox),
so `dotnet test` needs no migration step or local database install.

## Design rationale

Usage and the architecture overview live in the root `README.md`. The hard architectural invariants — the
handler pipeline running before storage, per-container blob-name uniqueness, blob/DB consistency, the
authorization model, and DI-lifetime discipline — live in
`framework/common/file-storing-invariants.md` (always loaded). Residual known limitations are noted inline in
the code (the concurrent-dedup and orphan-blob comments in `FileDescriptorManager`); the audit remediation is
tracked in the closed `audit`-labeled issues (#2–#35).
