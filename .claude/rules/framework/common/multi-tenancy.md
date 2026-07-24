---
paths:
  - "**/*Tenant*.cs"
  - "**/*MultiTenant*.cs"
  - "**/Entities/**/*.cs"
---

# ABP Multi-Tenancy

> **Docs**: https://abp.io/docs/latest/framework/architecture/multi-tenancy
>
> Generic ABP conventions first, then a per-module section. **Both modules deviate from the generic
> auto-population described here** — read your module's section.

## Making Entities Multi-Tenant

Implement `IMultiTenant` to make an entity tenant-aware:

```csharp
public class Product : AggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; } // Required by IMultiTenant

    public string Name { get; private set; }

    protected Product() { }

    public Product(Guid id, string name) : base(id)
    {
        Name = name;
        // TenantId is automatically set from CurrentTenant.Id in the generic case
    }
}
```

**Key points:**
- `TenantId` is **nullable** — `null` means the entity belongs to the Host.
- ABP **automatically filters** queries by the current tenant.
- ABP **automatically sets** `TenantId` when creating entities through the normal DI/UoW pipeline (generic case).

## Accessing Current Tenant

```csharp
var tenantId = CurrentTenant.Id;        // Guid? - null for host
var isAvailable = CurrentTenant.IsAvailable;
```

## Switching Tenant Context

```csharp
using (CurrentTenant.Change(tenantId))
{
    return await _someRepository.GetCountAsync();
}
```

## Disabling Multi-Tenant Filter

```csharp
using (DataFilter.Disable<IMultiTenant>())
{
    return await _someRepository.GetCountAsync(); // ALL tenants
}
```

## Best Practices

1. **Always implement `IMultiTenant`** for tenant-specific entities.
2. **Never manually filter by `TenantId`** — ABP does it automatically. (A module's custom repository methods
   may still take their own domain filters; tenant scoping stays ABP's job.)
3. **Don't change `TenantId` after creation** — it moves the entity between tenants.
4. **Use `Change()` scope carefully** — nested scopes are supported.
5. **Test both host and tenant contexts** — ensure proper data isolation.

## Tenant Resolution

ABP resolves the current tenant from (in order): the user's claims, query string, route, HTTP header, cookie,
domain/subdomain (if configured).

## Applies to both modules

Neither module relies on ABP's automatic `TenantId` population — in both, the aggregates' `TenantId` setter is
**protected**, so it is assigned **through the constructor** by the code that builds the entity, falling back
to the ambient tenant when the caller didn't specify one (`someTenantId ?? CurrentTenant.Id`). If you add a new
aggregate or a new insert path in either module, follow that pattern rather than assuming ABP will populate
`TenantId` for you. **Preserve tenant scope on every query, write, background job, and event.**

---

## In `file-storing`

Both aggregates implement `IMultiTenant` with a **protected** setter:

```csharp
public Guid? TenantId { get; protected set; }
```

`FileDescriptorManager` / `DirectoryManager` pass it in explicitly.

**Uniqueness and lookup indexes are tenant-scoped**: `FileDescriptor`'s unique blob-name index is
`(TenantId, ContainerName, BlobName)` and the filtered-unique MD5 index is `(TenantId, ContainerName, Md5)`.
**Never write a "global" uniqueness or dedup check that ignores `TenantId`** — it would leak or collide across
tenants. See `file-storing/.claude/rules/framework/data/ef-core.md` and `file-storing-invariants.md` §3.

The repository tests assert tenant scoping directly (e.g. `BlobNameExistsAsync_ShouldBeTenantScoped`) — that's
the guarantee protecting §3, so keep it passing.

---

## In `notifications`

All three aggregates (`Notification`, `UserNotification`, `NotificationSubscription`) implement `IMultiTenant`
explicitly:

```csharp
public virtual Guid? TenantId { get; protected set; }
```

`NotificationStore` passes it through each entity's constructor (`notification.TenantId ?? CurrentTenant.Id`).

**Tenant scope is authoritative during distribution.** Evaluate a notification in its **recorded `TenantId`**;
do not inherit an unrelated ambient tenant from an inline caller or background worker. Host (`null`) and tenant
contexts must never mix — `null` is *authoritative host*, not an instruction to fall back to ambient state.
Direct `INotificationDistributor` callers must populate tenant notifications explicitly.

Subscription uniqueness includes the tenant and the complete scope — see `notifications-invariants.md`
§6, §7 and §8.
