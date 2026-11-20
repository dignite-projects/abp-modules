# Usage by tenant, for the host; the management page in two (2026-11-20)

Status: the direction was decided by the owner on 2026-11-20 (a fourth breakdown, by tenant, for the host only; the
management page split into *Usage* and *Files*; every breakdown is read when it is asked for). The details under
*Design* are proposed from the code and ABP's own modules and wait for the owner's confirmation. Implemented the same
day, as written, in the working tree (not committed); the tests that cover it are `Usage_Should_Be_Told_By_Tenant_Across_Tenants`
and `A_Tenants_Allocation_Should_Be_Read_In_That_Tenant` (domain), `Usage_By_Tenant_Should_Be_Read_Only_By_The_Host`
(application) and `The_Host_Should_Read_The_Usage_By_Tenant_Over_HTTP`, and the component's spec.

Narrows [`positioning-decision.md`](positioning-decision.md) ("Usage"): the usage was the current tenant's, by
container, by user and by client, and each of them added up to the tenant's stored bytes. That stays true for every
tenant. What is added is the host's one view across tenants.

## The problem

- **The host cannot see who stores what.** `FileDescriptor` is `IMultiTenant`, so every usage query - the tenant's
  total, the containers', the users', the clients' - runs under ABP's tenant filter. In the host the filter shows only
  the host's own rows. The allocation (`BlobManagementFeatures.StorageSize`) is a per-tenant feature the host sets in
  Tenant Management, and the host has nothing to read it against: it cannot tell which tenant is near its limit, or
  which one holds most of the disk.
- **The page asks for everything on open, and a tab never asks again.** The totals and the container table are one call
  made in `ngOnInit`; the users and the clients are read the first time their tab is selected and kept for the life of
  the page, with no way to read them again short of reloading it. Each breakdown is a `GROUP BY` over the descriptor
  table, with no cache. A cross-tenant one is the most expensive of them, and a number that cannot be refreshed is no
  use to an administrator who has just watched a tenant delete a folder.
- **The page is one page.** The usage card sits above the container tabs and pushes the file list down; an application
  that wants one of the two has to replace both (`eBlobManagementComponents.BlobManagement`).

## How ABP does it

Read in `E:\github-code\abp` (LGPL-3.0; the designs are borrowed, no code is copied):

- **A permission for the host only** is a permission with `multiTenancySide: MultiTenancySides.Host`
  (`AbpTenantManagementPermissionDefinitionProvider`, `FeaturePermissionDefinitionProvider`). `PermissionChecker` answers
  `false` for it when the current side is a tenant, whoever holds it - it is never granted in a tenant, so no second
  check is needed in the service. This module already uses it for `BlobManagement.Container.Files`.
- **Reading across tenants** is `DataFilter.Disable<IMultiTenant>()` in a domain service around the repository call
  (`IdentityUserManager`), not a repository that knows about it. With EF Core the filter is read when the query runs
  (`AbpDbContext.IsMultiTenantFilterEnabled`) and with MongoDB when the queryable is created
  (`MongoDbRepository.ApplyDataFilters`); the repository call and its `await` are inside the `using` here, which
  covers both.
- **The name of a tenant** is `ITenantStore.FindAsync(Guid)` (`Volo.Abp.MultiTenancy.Abstractions`), the framework's own
  seam, which Tenant Management implements. This module does not reference Tenant Management: without a store that
  knows the tenant, a row is shown by its id - as a user is, without the user lookup.
- **Another tenant's feature** is read inside `CurrentTenant.Change(tenantId)` with `IFeatureChecker`: the feature's
  tenant value provider reads the current tenant, so the same call answers for the tenant switched to. No ABP module
  was found that does this; it is the framework's documented switch, used as it is meant.

Not found, and so stated as ours: no module of the open-source ABP clone reports the storage of each tenant, and ABP's
File Management and SaaS modules are commercial and were not read. The object stores the module was compared with in
[`peer-comparison.md`](peer-comparison.md) have projects and organisations, not the tenants of one deployment; none of
them was a source for this.

## Design

### The breakdown

`GET api/blob-management/usage/tenants` - `IBlobManagementUsageAppService.GetTenantsAsync(input)`, paged, the largest
first, ended by the tenant id so two pages never share or miss a tenant:

| Field | Meaning |
|---|---|
| `TenantId` | `null` is the host's own files |
| `TenantName` | from `ITenantStore`; `null` when there is no store that knows the tenant (it was deleted, or the host runs without Tenant Management) |
| `StoredBytes`, `FileCount` | the sizes and the number of the tenant's live files, in every container |
| `StorageSize` | the tenant's allocation in bytes, `null` when it has none - read per row of the page, so at most one page of feature reads |

The rows of all pages add up to every live descriptor of the deployment: the host's and the tenants' together. The
header of the usage page stays what it was - the **current** tenant's stored bytes against its allocation, which in the
host is the host's own - so the rows of this breakdown are not the header's number. The column says so.

Count and page are the repository's: `GetTenantUsagesAsync(skipCount, maxResultCount)` and `GetTenantCountAsync()` on
`IFileDescriptorRepository`, both of them across tenants *by contract*, and both called by
`BlobManagementUsageManager` inside `DataFilter.Disable<IMultiTenant>()`.

### Authorization

`BlobManagement.Containers.TenantUsages`, a child of the management permission, defined for the **host side only**
(`MultiTenancySides.Host`). `[Authorize(...)]` on `GetTenantsAsync` is all the service does; the class-level
`BlobManagement.Containers` and `[RequiresFeature(Enable)]` still apply, so the host's own Blob Management must be
enabled. A tenant's administrator never sees the permission in the dialog, and a host administrator who switches to a
tenant is refused, because the side is then a tenant. The permission only reads: it changes nothing, as the management
permission does not.

Not chosen: `BlobManagement.Containers` plus a check of `CurrentTenant.Id == null` in the service - two rules that say
one thing, and a tenant's administrator holds the first. A new top-level permission - the breakdown is a reading of
what the management permission reads, one level up.

### Every breakdown is read when it is asked for

- The usage page opens on **Containers**, which is read together with the totals and the allocation (one call, as
  today; the header needs them). That is the first selection, not a different rule.
- **Users**, **Clients** and **Tenants** are read the first time their tab is selected.
- Each tab has a **Refresh** button that reads that breakdown again - the containers' one the totals too. Nothing is
  read again on its own.
- The tenants tab exists only for a caller who holds `TenantUsages`: the UI asks `PermissionService` for the policy,
  which is false in a tenant by ABP's own rule.

No cache on the server: it would need an invalidation on every store and delete, and it would make a refresh button lie.
A `GROUP BY` over one table, read on demand by an administrator, is the cost this module has always accepted for usage.

### The page in two

| Menu item | Route | Policy | Component (replaceable key) |
|---|---|---|---|
| Blob Management (parent, under Administration) | | `BlobManagement.Containers` | |
| Usage | `/blob-management/usage` | `BlobManagement.Containers` | `BlobManagementUsageComponent` (`eBlobManagementComponents.Usage`) |
| Files | `/blob-management/files` | `BlobManagement.Containers` | `BlobManagementFilesComponent` (`eBlobManagementComponents.Files`) |

`/blob-management` redirects to `usage`. The component that was the whole page (`BlobManagementComponent`, the key
`eBlobManagementComponents.BlobManagement`, the route name `eBlobManagementRouteNames.BlobManagement`) becomes the
*Files* page: the containers' tabs, their directory tree and files, and the permissions dialog. The usage card becomes
the *Usage* page with its own card. The route name of the parent stays; the two children are new names. Nothing is
published, so this is no rename for a consumer.

## What changes

- `Application.Contracts`: `BlobManagementTenantUsageDto`, `GetBlobManagementTenantUsagesInput`,
  `GetTenantsAsync`, the permission and its definition (host side), `BlobManagementPermissions.GetAll()`.
- `Domain`: `BlobManagementTenantUsage`, the two repository methods, `BlobManagementUsageManager.GetTenantUsagesAsync`
  and `GetTenantCountAsync`; `EntityFrameworkCore` and `MongoDB`: the queries.
- `Application`, `HttpApi`, `HttpApi.Client`: the service method, the route, the C# proxy.
- Angular: the proxy, the two pages and their routes and menu, the tenants tab, the refresh buttons, the policy name.
- Localization in the 25 cultures; `docs/en/blob-management/`; `blob-management/CLAUDE.md`; the specs and tests.

## Still open

- The `StorageSize` column reads one feature per row. If a deployment has thousands of tenants and the page size is
  raised, that is the cost to look at first.
- A tenant that was deleted with files left behind shows by id and cannot be told from one whose name the store does not
  know. Cleaning up after a deleted tenant is not this module's.
