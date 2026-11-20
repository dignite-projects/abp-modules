```json
//[doc-seo]
{
    "Description": "Storage usage in Dignite Blob Management: stored bytes for the tenant, each container, each user and each OAuth client, and - for the host - each tenant; the usage endpoints and the tenant allocation."
}
```

# Usage

> Blob Management has not been released yet; see the [overview](index.md).

Every BLOB has a file descriptor, so Blob Management counts what the current tenant keeps from that table: the tenant, each container, each user and each client. The figures are for holders of `BlobManagement.Containers` ([Permissions](permissions.md)), and the [usage page](angular-ui.md#the-pages) shows them.

## Stored Bytes

Every figure is in **stored bytes**: the sizes of the files, as stored - after the container's pipeline contributors ran, so a JPEG the container keeps as WebP counts as its WebP. Every file has a physical BLOB of its own, also when another file holds the same bytes, so the same bytes kept in two files count twice. The containers add up to the tenant, and so do the users and the clients.

A user is the one a file is counted for, its `CreatorId` ([The Uploader](files.md#the-uploader)); a client is the OAuth client that stored it over HTTP, its `ClientId`. Files stored in Blob Management's own process have no client, and are gathered in one row. Deleted files do not count; files uploaded with a ticket and not confirmed yet do ([Upload Tickets](files.md#upload-tickets)).

## The API

`IBlobManagementUsageAppService`, in `Dignite.Abp.BlobManagement.Application.Contracts`, behind `BlobManagement.Containers`:

| | |
|---|---|
| `GET api/blob-management/usage` | `GetAsync`: the tenant's stored bytes, file count and allocation (`storageSize`, in bytes; none when it has no limit), and each container's stored bytes and file count. Every managed container is listed, empty ones too, and any other container that still holds files. |
| `GET api/blob-management/usage/users?containerName=` | `GetUsersAsync`: each user's stored bytes and file count, the largest first, a page at a time (`skipCount`, `maxResultCount`). `containerName` narrows it to one container. |
| `GET api/blob-management/usage/clients?containerName=` | `GetClientsAsync`: each client's stored bytes and file count, the largest first. |
| `GET api/blob-management/usage/tenants` | `GetTenantsAsync`, **for the host only** ([By Tenant](#by-tenant)): each tenant's stored bytes, file count and allocation, the largest first, a page at a time (`skipCount`, `maxResultCount`). |

A user row has `userId` and `userName`. The name comes from ABP's `IExternalUserLookupServiceProvider` where the host has one - its Identity module, or that module's HTTP client - and still knows the user; without it, the user is shown by id. Files recorded for no user are gathered in a row without `userId`.

## The Allocation

The allocation is the tenant's `BlobManagement.StorageSize` [feature](setting-up.md#tenant-storage), checked against the tenant's stored bytes. There is no allocation for a container or a user.

The figures above are the current tenant's. In the host they are the host's own files; [By Tenant](#by-tenant) is how the host sees every tenant's.

## By Tenant

Every other figure stops at the current tenant, because ABP filters what an entity of a tenant shows. The host sees what each tenant keeps, and what it keeps itself, in one list: `GET api/blob-management/usage/tenants`.

| Field | |
|---|---|
| `tenantId` | The tenant; none for the host's own files. |
| `tenantName` | The tenant's name, from ABP's tenant store (`ITenantStore`, which Tenant Management implements); none when the host has no store that knows the tenant - the tenant was deleted and left files behind - and then the tenant is shown by its id. |
| `storedBytes`, `fileCount` | The sizes and the number of the tenant's files, in every container. |
| `storageSize` | The tenant's allocation in bytes; none when it has no limit. |

It needs `BlobManagement.Containers.TenantUsages`, a child of the management permission that is defined for the **host side only**: ABP does not grant it in a tenant, and a tenant's administrator is refused whatever else they hold. It reads, as the management permission does, and changes nothing.

The rows of all the pages add up to every file of the deployment. They are not the numbers the usage page's header shows, which are the current tenant's.

## Reading It

The [usage page](angular-ui.md#the-pages) reads each breakdown when it is asked for: the containers' with the totals when it opens, the users', the clients' and the tenants' the first time their tab is selected, and every tab again when its refresh button is pressed. Nothing is cached: each is a sum over the descriptor table, computed when it is asked for.

## See Also

* [Setting Up](setting-up.md#tenant-storage)
* [Keeping Files by Id](files.md)
* [Angular UI](angular-ui.md)
