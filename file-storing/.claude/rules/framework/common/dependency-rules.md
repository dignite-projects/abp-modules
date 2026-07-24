---
paths:
  - "**/*.csproj"
  - "**/*Module*.cs"
---

# Dependency Rules

## Core Principles (Always Apply)

1. **Domain logic never depends on infrastructure** (no `DbContext` in Domain/Application).
2. **Depend on abstractions** (interfaces), not concrete implementations.
3. **Higher layers depend on lower layers**, never the reverse.
4. **Data access through repositories**, not direct `DbContext` — see `framework/data/ef-core.md` (this repo
   uses custom repositories, `IFileDescriptorRepository` / `IDirectoryDescriptorRepository`, backed by EF Core
   and MongoDB).

## This repo's actual dependency graph (two trees)

```
core/ — extends ABP BlobStoring, no DDD layers:

  AbpBlobStoringModule
        ▲
  Dignite.Abp.FileStoring            (IFileHandler pipeline: FileSizeLimit + FileTypeCheck handlers,
        ▲                             ContainerNameValidator, IBlobNameGenerator)
  Dignite.Abp.FileStoring.Imaging    (adds ImageResizeHandler; depends on Abp.Imaging + ImageSharp)


file-explorer/ — a DDD application built ON the core:

  FileExplorer.Domain.Shared ──▶ FileExplorer.Domain ──▶ (references Dignite.Abp.FileStoring core)
                                        │  aggregates: FileDescriptor, DirectoryDescriptor
                                        │  managers:   FileDescriptorManager, DirectoryManager
                                        │  repos:      IFileDescriptorRepository, IDirectoryDescriptorRepository
                    ┌───────────────────┼───────────────────────┐
                    ▼                    ▼                       ▼
     Application.Contracts      EntityFrameworkCore          MongoDB
      (permissions, DTOs)   (both implement the custom repository interfaces)
                    │
                    ▼
     Application ──▶ HttpApi (auto controllers) / HttpApi.Client (C# proxies)
        (Mapperly, Imaging)
```

Central rule specific to this repo: **`file-explorer` is an optional DDD application of the FileStoring core,
not a prerequisite for it.** The `core/` packages (`Dignite.Abp.FileStoring` + `.Imaging`) are usable on their
own — a consumer can configure blob containers with the `IFileHandler` pipeline and never touch
`file-explorer`. `file-explorer/Domain` references the core; the core must never reference `file-explorer`.

## Critical rules

### ❌ Never do

```csharp
// Application layer accessing DbContext directly
public class FileDescriptorAppService : ApplicationService
{
    private readonly FileExplorerDbContext _dbContext; // ❌ WRONG — use IFileDescriptorRepository
}

// The FileStoring core depending on file-explorer, EF Core, or MongoDB
// ❌ WRONG — the core only knows ABP BlobStoring + its own IFileHandler abstraction

// An IFileHandler in core depending on file-explorer aggregates/repositories
// ❌ WRONG — handlers see only the FileHandlerContext (stream + container configuration)
```

### ✅ Always do

```csharp
// Application/domain layer using the custom repository
public class FileDescriptorManager : DomainService
{
    private readonly IFileDescriptorRepository _fileDescriptorRepository; // ✅
}

// A handler works only against its context, so any channel/rule can be added without touching consumers
public class FileTypeCheckHandler : IFileHandler, ITransientDependency
{
    public Task ExecuteAsync(FileHandlerContext context) { /* inspect context.BlobStream / MimeType */ }
}
```

## Central package management

**Library package versions live in `Directory.Packages.props`** at the repo root
(`ManagePackageVersionsCentrally=true`). A library `.csproj` should only have
`<PackageReference Include="..." />` with **no `Version=`**. The demo `host/` is the deliberate exception —
`common.props` sets `ManagePackageVersionsCentrally=false` for it, and it pins inline. Adding/bumping a NuGet
version means editing `Directory.Packages.props` (or the host's own `.csproj`), not a library `.csproj`.

## Target framework — single-targeted `net10.0`

**Every project in this repo targets `net10.0` only** — including the contract layers (`Domain.Shared`,
`Application.Contracts`, `HttpApi.Client`). There is **no `netstandard` multi-targeting** here. Don't add a
`<TargetFrameworks>` (plural) list to a new project; match the existing single `<TargetFramework>net10.0`.

## Enforcement checklist when adding a feature

1. New upload rule/transform (size, type, image op)? → a new `IFileHandler` in `core/src/` + a
   `BlobContainerConfigurationExtensions` method to attach it per container. See "Adding a feature" in
   `template/app.md`.
2. New aggregate/entity? → `file-explorer/Domain` (+ Domain.Shared for constants).
3. New query? → a method on the aggregate's custom repository interface, implemented in **both**
   `EntityFrameworkCore` and `MongoDB`.
4. New DTO/service interface? → `Application.Contracts`; implementation in `Application` (map via Mapperly).
5. New permission? → `FileExplorerPermissions` + `FileExplorerPermissionDefinitionProvider` in
   `Application.Contracts`.
6. New package? → add the version to `Directory.Packages.props`, then reference it (no version) in the `.csproj`.
