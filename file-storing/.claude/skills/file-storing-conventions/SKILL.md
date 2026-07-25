---
name: file-storing-conventions
description: How the Dignite.Abp.FileStoring module applies ABP — the FileDescriptor/DirectoryDescriptor aggregate shapes, custom IBasicRepository-based repositories implemented in both EF Core and MongoDB, domain services, Mapperly mapping, conventional (auto) API controllers, FileExplorerPermissions and the two-layer resource-based authorization model, tenant-scoped uniqueness indexes, settings/features/caching/event-bus posture, error codes and localization. Read when writing or reviewing code under file-storing/ and the generic abp-* skill doesn't say what THIS module does.
---

# file-storing — Module Conventions

> Where this file and a generic `abp-*` skill disagree, **this file wins for code under `file-storing/`**.

## The two aggregates

```csharp
public class FileDescriptor : AggregateRoot<Guid>, ICreationAuditedObject, IDeletionAuditedObject, IMultiTenant
{
    public string ContainerName { get; protected set; } = default!;
    public string BlobName { get; protected set; } = default!;
    public string Name { get; protected set; } = default!;

    protected FileDescriptor() { } // For ORM

    public FileDescriptor(Guid id, string containerName, string blobName, string name, /* ... */ Guid? tenantId)
        : base(id)
    {
        ContainerName = containerName;
        BlobName = blobName;
        Name = name;
        TenantId = tenantId;
    }

    public void Rename(string name)
        => Name = Check.Length(name, nameof(name), FileConsts.MaxNameLength) ?? string.Empty;

    public void MoveToDirectory(Guid? directoryId) => DirectoryId = directoryId;
}
```

Repo-specific choices — **follow them, don't "normalize" them to a single generic pattern**:

- **`FileDescriptor : AggregateRoot<Guid>, ICreationAuditedObject, IDeletionAuditedObject, IMultiTenant`.**
  Business properties (`ContainerName`, `BlobName`, `Size`, `Name`, `MimeType`, `Md5`, `ReferBlobName`,
  `CellName`, `DirectoryId`, `EntityId`) have **protected** setters and are changed through behavior methods
  (`SetMd5`, `SetReferBlobName`, `SetSize`, `Rename`, `MoveToDirectory`, `SetCell`). The audit-interface
  properties (`CreationTime`/`CreatorId`/`DeletionTime`/`DeleterId`/`IsDeleted`) have public setters — ABP's
  auditing infrastructure sets those, that's expected. `TenantId` is `protected set`, assigned via the ctor.
- **`DirectoryDescriptor : AuditedAggregateRoot<Guid>, IMultiTenant`.** Note it currently exposes **public**
  setters on `Name`/`ParentId`/`Order`; its invariants (parent validity, no self/descendant move, non-empty
  deletion block) are enforced by `DirectoryManager`, not the aggregate. That public-setter surface is a known
  encapsulation gap flagged in the audit — prefer adding behavior methods over leaning on it, and never mutate
  `ParentId` directly to bypass the move rules (`file-storing-invariants` §6).

### Multi-tenancy on the aggregates

Both implement `IMultiTenant` with a **protected** setter; `FileDescriptorManager` / `DirectoryManager` pass it
in explicitly:

```csharp
public Guid? TenantId { get; protected set; }
```

**Uniqueness and lookup indexes are tenant-scoped**: `FileDescriptor`'s unique blob-name index is
`(TenantId, ContainerName, BlobName)` and the filtered-unique MD5 index is `(TenantId, ContainerName, Md5)`.
**Never write a "global" uniqueness or dedup check that ignores `TenantId`** — it would leak or collide across
tenants (`file-storing-invariants` §3). The repository tests assert this directly
(`BlobNameExistsAsync_ShouldBeTenantScoped`) — keep them passing.

## Custom repositories — both aggregates, both providers

```csharp
public interface IFileDescriptorRepository : IBasicRepository<FileDescriptor, Guid>
{
    Task<bool> BlobNameExistsAsync(string containerName, string blobName, CancellationToken ct = default);
    Task<FileDescriptor> FindByMd5Async(string containerName, string md5, CancellationToken ct = default);
    Task<List<FileDescriptor>> GetListAsync(/* container, creator, directory, filter, sorting, paging */);
}
// also: Md5ExistsAsync / ReferencingAnyAsync / FindByBlobNameAsync / GetCountAsync
// and IDirectoryDescriptorRepository : IBasicRepository<DirectoryDescriptor, Guid>
```

- They deliberately extend `IBasicRepository<T, Guid>` (**not** the LINQ-exposing `IRepository<T, Guid>`) and put
  every query behind a named method — blob-name/MD5 existence, referencing checks for reference-based dedup,
  filtered/sorted paging.
- Add a new query as a method on the matching interface and implement it in **both** the EF Core and MongoDB
  projects, registered via `options.AddRepository<TEntity, TImpl>()`. Don't dump ad-hoc LINQ into an AppService.
- These repositories take an explicit `CancellationToken` — pass it on.
- Both providers must apply the **same** default order (`CreationTime` descending), and any dynamic `sorting`
  string must go through a column allowlist (`file-storing-invariants` §8).

## Domain services

`FileDescriptorManager` and `DirectoryManager`:

```csharp
public class DirectoryManager : DomainService
{
    private readonly IDirectoryDescriptorRepository _directoryRepository;

    public async Task MoveAsync(DirectoryDescriptor directory, Guid? newParentId)
    {
        // Business rule: a directory may not move into itself or a descendant (would create a cycle)
        // Business rule: parent must exist and share tenant/owner/container
    }
}
```

Check DI lifetimes against `file-storing-invariants` §7 before making a manager `ISingletonDependency`.

### Domain events — this module publishes none

`AddLocalEvent()` is available, but this module's packages publish no local or distributed events. The upload
pipeline (`IFileHandler`) and the file/directory managers run **inline** within the request's unit of work.
Don't add an outbox/ETO unless a genuine cross-service need appears — and specifically don't add one to make
"write metadata" + "write blob" atomic; that's ordering and compensation (`file-storing-invariants` §4).

## Base classes and DI lifetimes

| Base class | Used by |
|---|---|
| `AggregateRoot<TKey>` | `FileDescriptor` (with `ICreationAuditedObject`/`IDeletionAuditedObject`) |
| `AuditedAggregateRoot<TKey>` | `DirectoryDescriptor` |
| `DomainService` | `FileDescriptorManager`, `DirectoryManager` |
| `ApplicationService` | `FileDescriptorAppService`, `DirectoryDescriptorAppService` |

The core's upload handlers (`FileSizeLimitHandler`, `FileTypeCheckHandler`, `ImageResizeHandler`) are **plain
classes** registered as `IFileHandler, ITransientDependency` — they inherit no ABP base class, so there is no
inherited `Clock`/`CurrentUser`/`L` to lean on:

```csharp
public class MyHandler : IFileHandler, ITransientDependency
{
    private readonly IClock _clock;
    public MyHandler(IClock clock) => _clock = clock;   // ✅ no base-class `Clock` property here
}
```

**Replaceable defaults**: `IBlobNameGenerator` (default `RandomBlobNameGenerator`) and the per-container,
opt-in `IFileDescriptorEntityAuthorizationHandler`.

## Application layer

### Object mapping runs on **Mapperly**, not AutoMapper

Mapping lives in `FileExplorerApplicationMappers.cs`. Each map is a compile-time Mapperly partial extending
ABP's `MapperBase<TSource, TDestination>`:

```csharp
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class FileDescriptorToDtoMapper : MapperBase<FileDescriptor, FileDescriptorDto>
{
    [MapperIgnoreTarget(nameof(FileDescriptorDto.Url))]   // Url is resolved per-request, not stored
    public override partial FileDescriptorDto Map(FileDescriptor source);
    public override partial void Map(FileDescriptor source, FileDescriptorDto destination);
}
```

- Consume it through ABP's `IObjectMapper`, registered by `AbpMapperlyModule` (a dependency of
  `FileExplorerApplicationModule`, wired via `AddMapperlyObjectMapper<FileExplorerApplicationModule>()`) —
  **don't** inject a mapper class directly and call it yourself. Per ABP's own docs this isn't just style:
  skipping `MapperBase`/`IObjectMapper` loses automatic collection mapping and the `BeforeMap`/`AfterMap` hooks,
  and breaks the "stays swappable" guarantee a published, reusable module is supposed to give consumers.
- `RequiredMappingStrategy.Target` makes any unmapped destination property a **build error**, so every new DTO
  field is either mapped by name or explicitly `[MapperIgnoreTarget]`-ed.
- **Computed, non-stored fields are ignored and filled in the AppService**: `FileDescriptorDto.Url` and
  `DirectoryDescriptorInfoDto.Children` (the directory tree, assembled by `DirectoryDescriptorAppService`).
- The repo was deliberately moved off AutoMapper to keep the published packages clear of AutoMapper's advisory
  (GHSA-rvv3-g6hj-g44x) and match ABP's own modules. **Don't reintroduce AutoMapper** or a hand-written
  `MapToDto` switch.

### Auto API controllers

AppServices are exposed as ABP **conventional (auto) API controllers** — there are no hand-written controllers.
`FileExplorerHttpApiModule` registers the application part and tunes the conventions:

```csharp
options.ConventionalControllers.FormBodyBindingIgnoredTypes.Add(typeof(CreateFileInput)); // multipart uploads
```

After changing a signature, regenerate the C# proxies in `HttpApi.Client` **and** the Angular proxy under
`angular/projects/file-explorer/src/lib/proxy` — the audit found real proxy/contract drift.

### Full update vs patch

Overwriting `DirectoryId`/`Name`/`CellName` unconditionally on a rename wipes fields the client never sent (the
Angular rename sends only `{ name }`). The update path was split into full-update vs patch for exactly this
reason, guarded by `Update.Tests` (`Rename_ShouldPreserveDirectoryAndCellName`) — `file-storing-invariants` §9.

## Authorization — two layers

```csharp
public static class FileExplorerPermissions
{
    public const string GroupName = "FileExplorer";

    public static class Files
    {
        public const string Default = GroupName + ".File";
        public const string Management = Default + ".Management";
    }
}
```

Registered in `FileExplorerPermissionDefinitionProvider`, localized through `FileExplorerResource`.

1. **Standard ABP permissions** gate coarse, admin-style actions — `FileExplorerPermissions.Files.Management`
   is the "manage anyone's files" permission.

2. **Resource-based authorization** decides access to an **individual** `FileDescriptor` /
   `DirectoryDescriptor`. `FileDescriptorAuthorizationHandler` and `DirectoryDescriptorAuthorizationHandler`
   are ASP.NET Core `AuthorizationHandler<OperationAuthorizationRequirement, TResource>` implementations
   (`CommonOperations.Get/Create/Update/Delete`). Which **permission** gates each operation is read
   **per blob container** from `BlobContainerAuthorizationConfiguration`
   (`GetFilePermissionName` / `CreateFilePermissionName` / `UpdateFilePermissionName` /
   `DeleteFilePermissionName`). A file is authorized when **any** of these hold:
   - no permission is configured **and** the operation is `Get` (public-read containers), or
   - the caller is the resource's `CreatorId`, or
   - the container's configured permission is granted, or
   - the global `FileExplorerPermissions.Files.Management` is granted.

   On top of that, a container may register a **pluggable per-entity handler**
   `IFileDescriptorEntityAuthorizationHandler` (`authorizationConfiguration.FileEntityAuthorizationHandler`).
   When a file is associated with a business entity (`EntityId`), that handler's
   `CheckAsync(fileDescriptor, requirement)` runs so the **consuming app** can authorize against its own
   entity (e.g. "can this user edit the product this image belongs to?"). The module ships the seam, the host
   supplies the policy.

**When gating a new file operation:**

- Container-wide access rules → set the permission names on the container's
  `BlobContainerAuthorizationConfiguration`, don't hard-code an `[Authorize]` on the handler path.
- Per-associated-entity rules → implement `IFileDescriptorEntityAuthorizationHandler` in the host and point the
  container's config at it.
- **Authorize every resource in a batch**, and never let temporary ownership precede the create check — both are
  real bypasses that the Authorization tests guard (`file-storing-invariants` §5).
- **`ContainerNameValidator` must actually validate** — an empty/no-op validator plus permissive defaults means
  an unregistered container is reachable by anyone who can guess a blob name.

## Infrastructure posture

- **Settings and Features — placeholders, not config surfaces.** `FileExplorerSettings` (Domain) +
  `FileExplorerSettingDefinitionProvider` exist but are mostly a placeholder (the group name is defined; there
  are no shipped settings yet) — add real settings there rather than inventing a new provider.
  **Per-container upload limits are NOT settings** — they're `BlobContainerConfiguration`. No Features are
  defined; the mechanism is available if a per-tenant toggle is genuinely needed.
- **Caching — the image-resize gap.** The on-the-fly image-resize endpoint has **no cache today**, so repeated
  resize requests re-decode and re-encode. If you add caching there, key it on the resolved (bounded) dimensions
  plus the source blob identity — **not** on raw client-supplied width/height.
- **Event bus** — see "Domain events — this module publishes none" above; the DbContext also has no
  transactional outbox/inbox (no `IHasEventInbox`/`IHasEventOutbox`).
- **Background jobs — none in the module packages.** If you introduce one (e.g. the orphan-blob cleanup sweep
  the audit recommends), carry the tenant on the args and re-establish it in the handler.

## Error codes and localization

Error codes live in `FileExplorerErrorCodes` / `FileErrorCodes`, and their namespace **must** match the
localization resource key namespace:

```csharp
[LocalizationResourceName("FileExplorer")]
public class FileExplorerResource { }

throw new BusinessException(FileExplorerErrorCodes.Directories.DirectoryNotExist).WithData("Name", name);

options.MapCodeNamespace("Dignite.FileExplorer", typeof(FileExplorerResource));
```

`FileExplorerErrorCodes` values, the `*.json` resource keys, and the `MapCodeNamespace(...)` mapping must agree —
a mismatch meant business exceptions didn't localize (fixed in "align file explorer error localization").
Resource files: `Dignite.FileExplorer.Domain.Shared/Dignite/FileExplorer/Localization/Resources/*.json`.

## Anti-patterns specific to this module

| Don't | Do instead |
|---|---|
| Trust the client-supplied MIME type / extension for validation | Detect the real content (`FileTypeCheckHandler`, image-format sniffing) — `file-storing-invariants` §2 |
| Buffer the whole upload into memory before enforcing the size limit | Enforce size at the HTTP layer and while streaming — `file-storing-invariants` §1 |
| Add an ETO/outbox to make metadata+blob writes atomic | Order the writes and compensate — `file-storing-invariants` §4 |
| Reintroduce AutoMapper or a hand-written `MapToDto` | Mapperly in `FileExplorerApplicationMappers` |
| Expose `IRepository<T, Guid>` / ad-hoc LINQ in an AppService | Add a named method to the custom repository, implement in EF Core **and** MongoDB |
