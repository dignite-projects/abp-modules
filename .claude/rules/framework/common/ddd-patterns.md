---
paths:
  - "**/*.Domain/**/*.cs"
  - "**/Domain/**/*.cs"
  - "**/Entities/**/*.cs"
---

# ABP DDD Patterns

> **Docs**: https://abp.io/docs/latest/framework/architecture/domain-driven-design
>
> Generic ABP conventions first, then a per-module section. The two modules have **genuinely
> different DDD shapes** — different aggregate base classes, different repository conventions,
> different event posture — so your module's section is not optional reading.

## Rich Domain Model vs Anemic Domain Model

ABP promotes the **Rich Domain Model** — entities hold data AND behavior:

| Anemic (Anti-pattern) | Rich (Recommended) |
|----------------------|-------------------|
| Entity = data only | Entity = data + behavior |
| Logic in services | Logic in entity methods |
| Public setters | Private/protected setters with methods |
| No validation in entity | Entity enforces invariants |

**Encapsulation is key**: protect state with private/protected setters, expose behavior through methods.

## Entities

```csharp
public class OrderLine : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public int Count { get; private set; }

    protected OrderLine() { } // For ORM

    internal OrderLine(Guid id, Guid productId, int count) : base(id)
    {
        ProductId = productId;
        SetCount(count); // Validates through method
    }

    public void SetCount(int count)
    {
        if (count <= 0)
            throw new BusinessException("Orders:InvalidCount");
        Count = count;
    }
}
```

## Aggregate Roots

Aggregate roots are consistency boundaries that own their child entities, enforce business rules, and can
publish domain events:

```csharp
public class Order : AggregateRoot<Guid>
{
    public string OrderNumber { get; private set; }
    public OrderStatus Status { get; private set; }

    protected Order() { } // For ORM

    public Order(Guid id, string orderNumber) : base(id)
    {
        OrderNumber = Check.NotNullOrWhiteSpace(orderNumber, nameof(orderNumber));
        Status = OrderStatus.Created;
    }

    public void Complete()
    {
        if (Status != OrderStatus.Created)
            throw new BusinessException("Orders:CannotCompleteOrder");

        Status = OrderStatus.Completed;
        AddLocalEvent(new OrderCompletedEvent(Id));                  // same transaction
        AddDistributedEvent(new OrderCompletedEto { OrderId = Id }); // cross-service
    }
}
```

### Domain Events
- `AddLocalEvent()` — handled within the same transaction, can access the full entity
- `AddDistributedEvent()` — handled asynchronously, via ETOs (Event Transfer Objects)

**Distributed-event posture differs sharply between the two modules** — `file-storing` publishes none by
design, `notifications` is built around one.

### Entity Best Practices
- **Encapsulation**: private/protected setters, public methods that enforce rules
- **Primary constructor**: enforce invariants, accept the `id` parameter
- **Protected parameterless constructor**: required for the ORM
- **Initialize collections**: in the primary constructor
- **Reference by Id**: don't add navigation properties to other aggregates
- **Don't generate GUIDs in the constructor**: use `IGuidGenerator` externally

## Repository Pattern

### When to Use Custom Repository
- **Generic repository** (`IRepository<T, TKey>`): sufficient for simple CRUD.
- **Custom repository**: when a query is reused across call sites.

**The two modules landed on opposite answers, on purpose:**

| Module | Convention |
|---|---|
| `file-storing` | **Custom repositories for both aggregates**, extending `IBasicRepository<T, Guid>`, every query behind a named method, implemented in EF Core **and** MongoDB. |
| `notifications` | **No custom repository interfaces.** All querying goes through the generic `IRepository<T, Guid>` inside the `INotificationStore` implementation. |

### Interface (Domain Layer)
```csharp
// Define a custom interface only when custom queries are needed
public interface IOrderRepository : IRepository<Order, Guid>
{
    Task<Order> FindByOrderNumberAsync(string orderNumber, bool includeDetails = false);
}
```

### Repository Best Practices
- **One repository per aggregate root only** — never create repositories for child entities. Child entities
  must be accessed/modified only through their aggregate root; a child repository bypasses the root's rules.
- ABP handles `CancellationToken` automatically; add the parameter for explicit cancellation control.
- Single-entity methods: `includeDetails = true` by default; list methods: `false`.
- Don't return projection classes.
- Interface in Domain, implementation in the data layer.

```csharp
// ✅ Correct: repository for the aggregate root
public interface IOrderRepository : IRepository<Order, Guid> { }

// ❌ Wrong: repository for a child entity — OrderLine belongs to the Order aggregate
public interface IOrderLineRepository : IRepository<OrderLine, Guid> { }
```

## Domain Services

Use domain services for business logic that spans multiple aggregates or needs repository queries to enforce
rules:

```csharp
public class OrderManager : DomainService
{
    private readonly IOrderRepository _orderRepository;

    public OrderManager(IOrderRepository orderRepository) => _orderRepository = orderRepository;

    public async Task<Order> CreateAsync(string orderNumber)
    {
        // Business rule: order number must be unique
        var existing = await _orderRepository.FindByOrderNumberAsync(orderNumber);
        if (existing != null)
        {
            throw new BusinessException("Orders:OrderNumberAlreadyExists")
                .WithData("OrderNumber", orderNumber);
        }

        return new Order(GuidGenerator.Create(), orderNumber);
    }
}
```

### Domain Service Best Practices
- Use the `*Manager` suffix
- No interface by default (create one only if needed)
- Accept/return domain objects, not DTOs
- Don't depend on the authenticated user — pass values from the application layer
- Use base class properties (`GuidGenerator`, `Clock`) instead of injecting these services
- **Check the DI lifetime of anything you inject** before marking a manager `ISingletonDependency` — see your
  module's invariants file (`file-storing-invariants.md` §7 / `notifications-invariants.md` §2)

## Specifications

Reusable query conditions:
```csharp
public class CompletedOrdersSpec : Specification<Order>
{
    public override Expression<Func<Order, bool>> ToExpression()
        => o => o.Status == OrderStatus.Completed;
}
```

---

## In `file-storing`

### The two aggregates

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
  `ParentId` directly to bypass the move rules.
- **Reference by Id**: `FileDescriptor.DirectoryId` is a plain `Guid?`, not a `DirectoryDescriptor` navigation.

### Domain events — this module publishes none
`AddLocalEvent()` is available, but this module's packages don't publish local or distributed events. The
upload pipeline (`IFileHandler`) and the file/directory managers run **inline** within the request's unit of
work. Don't add an outbox/ETO unless a genuine cross-service need appears — and specifically don't add one to
make "write metadata" + "write blob" atomic; that's handled by ordering and compensation
(`file-storing-invariants.md` §4).

### Custom repositories exist for both aggregates
```csharp
public interface IFileDescriptorRepository : IBasicRepository<FileDescriptor, Guid>
{
    Task<bool> BlobNameExistsAsync(string containerName, string blobName, CancellationToken ct = default);
    Task<FileDescriptor> FindByMd5Async(string containerName, string md5, CancellationToken ct = default);
    Task<List<FileDescriptor>> GetListAsync(/* container, creator, directory, filter, sorting, paging */);
}
```

`IFileDescriptorRepository` and `IDirectoryDescriptorRepository` extend `IBasicRepository<T, Guid>` (not the
LINQ-exposing `IRepository<T, Guid>`) and put every query behind a named method — blob-name/MD5 existence,
referencing checks for reference-based dedup, filtered/sorted paging.

- Add a new query as a method on the matching interface and implement it in **both** the EF Core and MongoDB
  projects. Don't dump ad-hoc LINQ into an AppService.
- These repositories take an explicit `CancellationToken` — pass it on.

### Domain services
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

Check DI lifetimes against `file-storing-invariants.md` §7 before making a manager `ISingletonDependency`.

---

## In `notifications`

### The three aggregates deviate from the generic template — on purpose
`Notification`, `UserNotification`, and `NotificationSubscription` deviate in ways that are **intentional —
follow them, don't "fix" them back to the generic pattern**:

- They inherit `BasicAggregateRoot<Guid>` (not `AggregateRoot<Guid>` or `AuditedAggregateRoot<Guid>`) and
  implement `IMultiTenant` **explicitly** (`public virtual Guid? TenantId { get; protected set; }`) rather than
  relying on a richer built-in base class — `CreationTime` is likewise a plain modeled property, not ABP's
  audited-entity convention.
- Setters are `protected` (not `private`), since these entities live in the same project/assembly as the code
  that constructs them. The pattern is otherwise the same: constructor + behavior method (e.g.
  `UserNotification.SetState(...)`) rather than public setters.

### No custom repository interfaces exist for any of the three
All querying (including multi-field filters like "this user's unread notifications since date X") is written
directly against the generic `IRepository<T, Guid>` inside the `INotificationStore` implementation.

Follow this for new queries against these aggregates. Only reach for a custom repository interface for a
genuinely new aggregate that needs the same query from multiple call sites.

### Domain events — one distributed event, with strict rules
This module's cross-cutting event is **`NotificationDeliveryRequestedEto`**. Before touching it, read
`notifications-invariants.md` §1 (serialization — the ETO carries the payload pre-serialized as `DataJson`,
never a polymorphic member) and §4 (single-recipient, best-effort, and cancellation guarantees).

### Domain services
`NotificationDefinitionManager`, `UserNotificationManager`, `NotificationSubscriptionManager`.

Check DI lifetimes against `notifications-invariants.md` §2 before marking a manager `ISingletonDependency` —
that exact mistake (a singleton manager injecting the scoped `INotificationStore`) is this module's motivating
bug.
