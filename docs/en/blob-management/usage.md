```json
//[doc-seo]
{
    "Description": "Storage usage in Dignite Blob Management: stored bytes for the tenant and each container, uploaded bytes for each user and each OAuth client, the three usage endpoints and the tenant allocation."
}
```

# Usage

> Blob Management has not been released yet; see the [overview](index.md).

Every BLOB has a file descriptor, so Blob Management counts what the current tenant keeps from that table: the tenant, each container, each user and each client. The figures are for holders of `BlobManagement.Containers` ([Permissions](permissions.md)), and the [management page](angular-ui.md#the-management-page) shows them.

## Two Measures

| Figure | Measure |
|---|---|
| The tenant, each container | **Stored bytes**: each physical BLOB once, however many files share it. Content is shared within a container only, so the containers add up to the tenant. |
| Each user, each client | **Uploaded bytes**: the sizes of their files. Two users who uploaded the same bytes are each counted for them, so these do not add up to the stored bytes. |

A user is the one a file is counted for, its `CreatorId` ([The Uploader](files.md#the-uploader)); a client is the OAuth client that stored it over HTTP, its `ClientId`. Files stored in Blob Management's own process have no client, and are gathered in one row. Deleted files do not count; files uploaded with a ticket and not confirmed yet do ([Upload Tickets](files.md#upload-tickets)).

## The API

`IBlobManagementUsageAppService`, in `Dignite.Abp.BlobManagement.Application.Contracts`, behind `BlobManagement.Containers`:

| | |
|---|---|
| `GET api/blob-management/usage` | `GetAsync`: the tenant's stored bytes, file count and allocation (`storageSize`, in bytes; none when it has no limit), and each container's stored bytes and file count. Every managed container is listed, empty ones too, and any other container that still holds files. |
| `GET api/blob-management/usage/users?containerName=` | `GetUsersAsync`: each user's uploaded bytes and file count, the largest first, a page at a time (`skipCount`, `maxResultCount`). `containerName` narrows it to one container. |
| `GET api/blob-management/usage/clients?containerName=` | `GetClientsAsync`: each client's uploaded bytes and file count, the largest first. |

A user row has `userId` and `userName`. The name comes from ABP's `IExternalUserLookupServiceProvider` where the host has one - its Identity module, or that module's HTTP client - and still knows the user; without it, the user is shown by id. Files recorded for no user are gathered in a row without `userId`.

## The Allocation

The allocation is the tenant's `BlobManagement.StorageSize` [feature](setting-up.md#tenant-storage), checked against the tenant's stored bytes. There is no allocation for a container or a user.

The figures are the current tenant's. The host does not see every tenant's usage in one place.

## See Also

* [Setting Up](setting-up.md#tenant-storage)
* [Keeping Files by Id](files.md)
* [Angular UI](angular-ui.md)
