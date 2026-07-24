---
paths:
  - "**/*Tenant*.cs"
  - "**/*MultiTenant*.cs"
  - "**/Entities/**/*.cs"
---

# ABP Multi-Tenancy

> **Docs**: https://abp.io/docs/latest/framework/architecture/multi-tenancy

## Making Entities Multi-Tenant

Implement `IMultiTenant` interface to make entities tenant-aware:

```csharp
public class Product : AggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; } // Required by IMultiTenant

    public string Name { get; private set; }

    protected Product() { }

    public Product(Guid id, string name) : base(id)
    {
        Name = name;
        // TenantId is automatically set from CurrentTenant.Id
    }
}
```

**Key points:**
- `TenantId` is **nullable** - `null` means entity belongs to Host
- ABP **automatically filters** queries by current tenant
- ABP **automatically sets** `TenantId` when creating entities through the normal DI/UoW pipeline

## Accessing Current Tenant

```csharp
public class ProductAppService : ApplicationService
{
    public async Task DoSomethingAsync()
    {
        var tenantId = CurrentTenant.Id;        // Guid? - null for host
        var isAvailable = CurrentTenant.IsAvailable;
    }
}
```

## Switching Tenant Context

```csharp
using (CurrentTenant.Change(tenantId))
{
    return await _productRepository.GetCountAsync();
}
```

## Disabling Multi-Tenant Filter

```csharp
using (DataFilter.Disable<IMultiTenant>())
{
    return await _productRepository.GetCountAsync(); // ALL tenants
}
```

## Best Practices

1. **Always implement `IMultiTenant`** for tenant-specific entities
2. **Never manually filter by `TenantId`** - ABP does it automatically
3. **Don't change `TenantId` after creation** - it moves entity between tenants
4. **Use `Change()` scope carefully** - nested scopes are supported
5. **Test both host and tenant contexts** - ensure proper data isolation

## Tenant Resolution

ABP resolves current tenant from (in order): current user's claims, query string, route, HTTP
header, cookie, domain/subdomain (if configured).

---

## In this repo

All three `NotificationCenter.Domain` aggregates (`Notification`, `UserNotification`,
`NotificationSubscription`) implement `IMultiTenant` explicitly:

```csharp
public virtual Guid? TenantId { get; protected set; }
```

Unlike the generic example above, `TenantId` is **not** auto-populated by ABP's conventions here —
`NotificationStore` (the `INotificationStore` implementation) passes it explicitly through each
entity's constructor, falling back to the ambient tenant when the caller didn't specify one:

```csharp
notification.TenantId ?? CurrentTenant.Id
```

If you add a new aggregate or a new insert path, follow this same explicit
`somethingId ?? CurrentTenant.Id` pattern rather than assuming ABP will populate `TenantId` for
you — these entities' setters are `protected`, set only through their constructors.
