# Dignite.FileExplorer

File-upload framework for ABP Framework (LGPL-3.0-only) on ABP BlobStoring, plus an optional DDD File
Explorer backend (directories + file metadata + REST API) and an Angular UI library. The module's
ABP Studio identity is `Dignite.FileExplorer` (the app is the install entry point — `core/` is an
internal dependency of it, not a separately installed thing). Published: `core/src/` (FileStoring +
Imaging), `file-explorer/src/` (the DDD app + Installer), `angular/projects/file-explorer`. `host/`
and the Angular demo app are local-dev-only, never packed.

## Structure

One `.slnx` — `Dignite.FileExplorer.slnx`:

- **`core/`** — `Dignite.Abp.FileStoring` (+ `.Imaging`). No DDD layers. Adds the `IFileHandler`
  upload pipeline to blob containers.
- **`file-explorer/`** — DDD app on core: `Domain.Shared, Domain, Application.Contracts,
  Application, HttpApi, HttpApi.Client, EntityFrameworkCore, MongoDB`. `FileDescriptor` /
  `DirectoryDescriptor` aggregates, conventional (auto) API controllers under `/api/file-explorer`.
- **`host/`** — demo ABP MVC host, in the `.slnx` but never packed:
  `dotnet run --project host/Dignite.FileExplorer.Web.Host` → `https://localhost:44390`.
- **`angular/`** — publishable `file-explorer` lib (`@dignite/ng.file-explorer` on npm, with a
  `/config` secondary entry point) + demo app, npm-only, not in the `.slnx`, `http://localhost:4200`.

`core/` never references `file-explorer/` — enforced by project references, not the solution file.

Namespace-mirrored files: `<Project>/<namespace path>/File.cs`, `<RootNamespace/>` empty. Put a new
file at the folder path matching its namespace (test projects that flatten to the project root are
the exception).

| Project | Responsibility | Depends on |
|---|---|---|
| `Abp.FileStoring` (Core) | `IFileHandler` pipeline, container config, blob naming | ABP BlobStoring |
| `Abp.FileStoring.Imaging` | `ImageResizeHandler` | Core, ImageSharp |
| `FileExplorer.Domain.Shared` | Constants, error codes, localization, settings | — |
| `FileExplorer.Domain` | Aggregates, managers, repository interfaces | Domain.Shared, FileStoring Core |
| `FileExplorer.Application.Contracts` | DTOs, service interfaces, permissions | Domain.Shared |
| `FileExplorer.Application` | AppServices, Mapperly mapping, authorization | Application.Contracts, Domain, Imaging |
| `FileExplorer.HttpApi` / `.HttpApi.Client` | Auto API controllers / client proxies | Application.Contracts |
| `FileExplorer.EntityFrameworkCore` / `.MongoDB` | Repository implementations | Domain |
| `FileExplorer.Installer` | ABP Studio/Suite install entry point, embeds the module's `.abpmdl` | `Volo.Abp.VirtualFileSystem` |

Tests by project: `Dignite.Abp.FileStoring[.Imaging].Tests` (core pipeline) · `FileExplorer.TestBase`
(abstract repository scenarios) · `.EntityFrameworkCore.Tests` / `.MongoDB.Tests` (those scenarios per
provider) · `.Domain.Tests` · `.Application.Tests` · `.Authorization.Tests` · `.DirectorySafety.Tests`
· `.Update.Tests`.

## The `IFileHandler` pipeline

```csharp
public interface IFileHandler
{
    Task ExecuteAsync(FileHandlerContext context); // FileName, MimeType, mutable BlobStream, container config
}
```

Attached via `BlobContainerConfigurationExtensions`, stored as an ordered `TypeList<IFileHandler>`:

```csharp
options.Containers.Configure<MyPicturesContainer>(c =>
{
    c.AddFileSizeLimitHandler(h => h.SetMaximumFileSize(2 * 1024 * 1024));
    c.AddFileTypeCheckHandler(h => h.SetAllowableFileTypeNames(".png", ".jpg"));
    c.AddImageResizeHandler(h => /* preset */);
});
```

`FileDescriptorManager` runs each handler's `ExecuteAsync` over the stream **before** the blob is
stored. Validators inspect (`FileSizeLimitHandler`, `FileTypeCheckHandler`); transforms replace
`context.BlobStream` (`ImageResizeHandler`). New upload rules = new handlers.

## Two operation modes

1. **Core only** — `Dignite.Abp.FileStoring` (+ `.Imaging`): blob containers with the handler
   pipeline, no directories, no persistence, no REST API.
2. **Full FileExplorer** — + `file-explorer` (+ EF Core or MongoDB): `DirectoryDescriptor` trees,
   persisted `FileDescriptor` metadata, authorization, REST API, Angular UI.

Core must keep working standalone.

## Adding a feature

**New upload rule/transform** (no entity, no DDD layer):
1. `IFileHandler` impl in `core/src/Dignite.Abp.FileStoring` (or `.Imaging`), `ITransientDependency`.
   Flow cancellation to any I/O.
2. `*Configuration` + `*ConfigurationNames`, and an `Add…Handler(...)` extension that `TryAdd<>`s it
   under `BlobContainerConfigurationNames.FileHandlers`.

## Commands

```bash
dotnet build Dignite.FileExplorer.slnx
dotnet test Dignite.FileExplorer.slnx

# Core only, skips embedded-mongod tests:
dotnet test core/test/Dignite.Abp.FileStoring.Tests

dotnet pack Dignite.FileExplorer.slnx -c Release

cd angular && npx yarn && npx yarn build:lib && npx yarn start                   # :4200
dotnet run --project host/Dignite.FileExplorer.Web.Host                          # :44390
```

No migrations ship in the library projects — a consuming host owns its own DbContext/migrations.