```json
//[doc-seo]
{
    "Description": "Dignite Blob Management permissions: the management permission BlobManagement.Containers, the directory permissions module-wide and granted on one container, and how applications are authorized by the container's allowed clients instead."
}
```

# Permissions

> Blob Management has not been released yet; see the [overview](index.md).

Blob Management's permissions authorize **users**, for the two things a user does here: read what the tenant stores - the [management page](angular-ui.md#the-management-page), the [usage](usage.md), the [MCP tools](mcp.md) - and keep the [directories](directories.md) of a container.

Files are an application's. An application that stores through the [provider](provider.md) or the [file service](files.md) is authorized by the container's `AllowedClientIds` - or trusted, in the same process - and uses none of these permissions; its own rules run before it calls. No permission lets a user store, rename, move or delete a file.

There are two levels: **module-wide** permissions, granted to roles and users in ABP's permission dialog, and the same **granted on one container**.

## Module-Wide Permissions

In the `BlobManagement` group of ABP's permission management:

| Permission | |
|---|---|
| `BlobManagement.Containers` | The management permission: enter the management page and read everything there - the usage, every container, its directories, its files and their bytes. |
| `BlobManagement.Containers.CreateDirectories` | Create directories in every container. |
| `BlobManagement.Containers.UpdateDirectories` | Rename and move directories. |
| `BlobManagement.Containers.DeleteDirectories` | Delete (empty) directories. |
| `BlobManagement.ManagePermissions` | Manage the grants on containers. An administrator's permission: whoever holds it can grant themselves anything. |

## Grants on a Container

A [resource permission](https://abp.io/docs/latest/modules/permission-management) is granted on **one** container - to a role or a user, in ABP's resource permission dialog - by a holder of `BlobManagement.ManagePermissions`. The resource is `Dignite.Abp.BlobManagement.BlobManagementContainer`, keyed by the container's name:

| Permission | |
|---|---|
| `BlobManagement.Container.View` | See the container's directory tree. The other three count only with it, or with the management permission. |
| `BlobManagement.Container.CreateDirectories` | Create directories in it. |
| `BlobManagement.Container.UpdateDirectories` | Rename and move its directories. |
| `BlobManagement.Container.DeleteDirectories` | Delete its directories. |

A grant on a container is the module-wide directory permission limited to that container. This is how a role keeps the directories of one container without the management permission: grant it `BlobManagement.Container.View` and the directory permissions it needs on that container.

## Who May Do What

| | Allowed with |
|---|---|
| Read the usage, and list and read files | `BlobManagement.Containers` |
| See a container's directory tree | `BlobManagement.Containers`, or `BlobManagement.Container.View` on it |
| Create, rename and move, delete directories | Seeing the tree, and the module-wide permission or the one granted on the container |
| Manage the grants on a container | `BlobManagement.ManagePermissions` |

* The answer is the **container's**, the same for every directory in it: no directory has permissions of its own, and no file has any.
* The **list of containers** is those the caller holds `BlobManagement.Containers` for, or any grant on. **One container's limits**, by its name, are read by every signed-in user.
* A token without a user is allowed none of these, and nobody is allowed any of them while the tenant's Blob Management is off ([The Tenant Feature](files.md#the-tenant-feature)).

Each request asks the grant store once for each container it answers about, never once per directory or file. What the caller may do with a container's directories is sent with every container as `access`.

## What It Needs

ABP's [Permission Management](https://abp.io/docs/latest/modules/permission-management) with its resource permissions, in the host that runs Blob Management; the Angular resource permission dialog comes from `@abp/ng.permission-management`. A Blob Management that serves only applications needs none of this, and runs without Permission Management. When it runs as [its own service](hosting.md#blob-management-as-its-own-service), it needs the permission data the other services share.

## See Also

* [Directories](directories.md)
* [Usage](usage.md)
* [Angular UI](angular-ui.md)
