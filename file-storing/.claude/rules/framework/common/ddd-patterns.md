---
paths:
  - "**/*.Domain/**/*.cs"
  - "**/Domain/**/*.cs"
  - "**/Entities/**/*.cs"
---

# ABP DDD Patterns

> **Docs**: https://abp.io/docs/latest/framework/architecture/domain-driven-design

## Rich Domain Model vs Anemic Domain Model

ABP promotes the **Rich Domain Model** — entities hold data AND behavior:

| Anemic (Anti-pattern) | Rich (Recommended) |
|----------------------|-------------------|
| Entity = data only | Entity = data + behavior |
| Logic in services | Logic in entity methods |
| Public setters | Private/protected setters with methods |
| No validation in entity | Entity enforces invariants |

**Encapsulation is key**: protect state with protected setters, expose behavior through methods.

## Entities

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

## Aggregate Roots

Aggregate roots are consistency boundaries that own child entities, enforce business rules, and can publish
domain events:

```csharp
public void Complete()
{
    if (Status != Status.Created)
        throw new BusinessException("SomeModule:CannotComplete");

    Status = Status.Completed;
    AddLocalEvent(new SomethingCompletedEvent(Id));          // same transaction
    // AddDistributedEvent(...) — cross-service; this repo's modules don't use distributed events
}
```

### Domain Events
- `AddLocalEvent()` - Handled within same transaction, can access full entity
- `AddDistributedEvent()` - Handled asynchronously via ETOs. **This repo's modules don't publish distributed
  events** — the upload pipeline (`IFileHandler`) and directory/file managers run inline within the request's
  unit of work. Don't add an outbox/ETO unless a genuine cross-service need appears.

### Entity Best Practices
- **Encapsulation**: protected setters, public methods that enforce rules
- **Primary constructor**: enforce invariants, accept `id` parameter
- **Protected parameterless constructor**: required for ORM
- **Reference by Id**: don't add navigation properties to other aggregates (e.g. `FileDescriptor.DirectoryId`
  is a plain `Guid?`, not a `DirectoryDescriptor` navigation)
- **Don't generate GUID in constructor**: use `IGuidGenerator` externally

## Repository Pattern

### When to Use Custom Repository
- **Generic repository** (`IRepository<T, TKey>`): fine for simple CRUD.
- **Custom repository**: when a query is reused across call sites. **This repo uses custom repositories** (see
  "In this repo").

### Interface (Domain Layer)
```csharp
public interface IFileDescriptorRepository : IBasicRepository<FileDescriptor, Guid>
{
    Task<bool> BlobNameExistsAsync(string containerName, string blobName, CancellationToken ct = default);
    Task<FileDescriptor> FindByMd5Async(string containerName, string md5, CancellationToken ct = default);
    Task<List<FileDescriptor>> GetListAsync(/* container, creator, directory, filter, sorting, paging */);
}
```

### Repository Best Practices
- **One repository per aggregate root only** — never for child entities.
- Define custom repository only when custom queries are needed.
- ABP handles `CancellationToken` automatically; these repositories still take an explicit one — pass it on.
- Single-entity methods: `includeDetails = true` by default; list methods: `false`.
- Don't return projection classes.
- Interface in Domain, implementation in the data layer (EF Core **and** MongoDB).

## Domain Services

Use domain services for logic that spans aggregates or needs repository queries to enforce rules. This repo's
domain services are `FileDescriptorManager` and `DirectoryManager`:

```csharp
public class DirectoryManager : DomainService
{
    private readonly IDirectoryDescriptorRepository _directoryRepository;

    public DirectoryManager(IDirectoryDescriptorRepository directoryRepository)
        => _directoryRepository = directoryRepository;

    public async Task MoveAsync(DirectoryDescriptor directory, Guid? newParentId)
    {
        // Business rule: a directory may not move into itself or a descendant (would create a cycle)
        // Business rule: parent must exist and share tenant/owner/container
        // ...
    }
}
```

### Domain Service Best Practices
- Use `*Manager` suffix (`FileDescriptorManager`, `DirectoryManager`)
- No interface by default (create only if needed)
- Accept/return domain objects, not DTOs
- Don't depend on the authenticated user — pass values from the application layer
- Use base class properties (`GuidGenerator`, `Clock`) instead of injecting these
- **Check the DI lifetime of anything you inject** — see `file-storing-invariants.md` before making a manager
  `ISingletonDependency`

---

## In this repo

The two `file-explorer/Domain` aggregates follow the rich model, with a couple of repo-specific choices —
**follow them, don't "normalize" them to a single generic pattern**:

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
  `ParentId` directly to bypass the move rules.
- **Custom repositories exist for both aggregates** (`IFileDescriptorRepository`,
  `IDirectoryDescriptorRepository`), extending `IBasicRepository<T, Guid>` and putting every query behind a
  named method (blob-name/MD5 existence, referencing checks for reference-based dedup, filtered/sorted paging).
  Add a new query as a method on the matching interface and implement it in **both** the EF Core and MongoDB
  projects — see `framework/data/ef-core.md`. Don't dump ad-hoc LINQ into an AppService.
