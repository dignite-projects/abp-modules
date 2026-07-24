---
paths:
  - "**/*Tenant*.cs"
  - "**/*MultiTenant*.cs"
  - "**/Entities/**/*.cs"
---

# ABP Multi-Tenancy

> **Docs**: https://abp.io/docs/latest/framework/architecture/multi-tenancy

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
    return await _fileDescriptorRepository.GetCountAsync(containerName, null, null);
}
```

## Disabling Multi-Tenant Filter

```csharp
using (DataFilter.Disable<IMultiTenant>())
{
    return await _fileDescriptorRepository.GetCountAsync(containerName, null, null); // ALL tenants
}
```

## Best Practices

1. **Always implement `IMultiTenant`** for tenant-specific entities.
2. **Never manually filter by `TenantId`** — ABP does it automatically. (The custom repository methods here
   still take `containerName`/`creatorId` filters, but tenant scoping stays ABP's job.)
3. **Don't change `TenantId` after creation** — it moves the entity between tenants.
4. **Use `Change()` scope carefully** — nested scopes are supported.
5. **Test both host and tenant contexts** — the repository tests assert tenant scoping (e.g.
   `BlobNameExistsAsync_ShouldBeTenantScoped`); keep that guarantee.

## Tenant Resolution

ABP resolves the current tenant from (in order): the user's claims, query string, route, HTTP header, cookie,
domain/subdomain (if configured).

---

## In this repo

Both aggregates (`FileDescriptor`, `DirectoryDescriptor`) implement `IMultiTenant` with a **protected** setter:

```csharp
public Guid? TenantId { get; protected set; }
```

Because the setter is protected, `TenantId` is assigned **through the constructor**, not by ABP's
auto-population — the `FileDescriptorManager` / `DirectoryManager` pass it in (falling back to the ambient
tenant when the caller didn't specify one, `someTenantId ?? CurrentTenant.Id`). If you add a new aggregate or
a new insert path, follow that same explicit `?? CurrentTenant.Id` pattern rather than assuming ABP will
populate `TenantId` for you.

Uniqueness and lookup indexes are **tenant-scoped**: e.g. `FileDescriptor`'s unique blob-name index is
`(TenantId, ContainerName, BlobName)` and the filtered-unique MD5 index is `(TenantId, ContainerName, Md5)`.
Never write a "global" uniqueness or dedup check that ignores `TenantId` — it would leak or collide across
tenants. See `framework/data/ef-core.md`.
