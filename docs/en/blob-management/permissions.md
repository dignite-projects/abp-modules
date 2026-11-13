```json
//[doc-seo]
{
    "Description": "Dignite Blob Management permissions: the module-wide BlobManagement.Containers permissions, grants on one container and on one restricted directory, authorization by place, reading a file by its id, and the uploader deleting their own file."
}
```

# Permissions

> Blob Management has not been released yet; see the [overview](index.md).

Everything people do - the [file API](attaching-files.md), the [directories](directories.md), the back office and the [MCP tools](mcp.md) - is authorized by Blob Management's own permissions, never by another module's: whoever should work with files in a container is granted Blob Management's permissions, as a role holds the permissions of every service it uses. Applications that store through the [provider](provider.md) are authorized by the container's `AllowedClientIds` instead, and use none of this.

There are three levels, each the one above limited to less: **module-wide** permissions, granted to roles and users in ABP's permission dialog; **grants on one container**; and **grants on one directory** that has permissions of its own.

## Module-Wide Permissions

In the `BlobManagement` group of ABP's permission management:

| Permission | |
|---|---|
| `BlobManagement.Containers` | The management permission: enter the back office and see every container, with its directories and files - restricted directories included. A high, top-level permission. |
| `BlobManagement.Containers.UploadFiles` | Upload files in every container. |
| `BlobManagement.Containers.UpdateFiles` | Rename and move files. |
| `BlobManagement.Containers.DeleteFiles` | Delete any file, not only one's own uploads. |
| `BlobManagement.Containers.CreateDirectories` | Create directories. |
| `BlobManagement.Containers.UpdateDirectories` | Rename and move directories. |
| `BlobManagement.Containers.DeleteDirectories` | Delete (empty) directories. |
| `BlobManagement.ManagePermissions` | Manage the grants on containers and directories, and say which directories are restricted. An administrator's permission: whoever holds it can grant themselves anything. |

## Grants on a Container

A [resource permission](https://abp.io/docs/latest/modules/permission-management) is granted on **one** container - to a role or a user, in ABP's resource permission dialog - by a holder of `BlobManagement.ManagePermissions`. The resource is `Dignite.Abp.BlobManagement.BlobManagementContainer`, keyed by the container's name:

| Permission | |
|---|---|
| `BlobManagement.Container.View` | See the container, its directories and the files in them. |
| `BlobManagement.Container.UploadFiles` | Upload files in it. |
| `BlobManagement.Container.UpdateFiles` | Rename and move its files. |
| `BlobManagement.Container.DeleteFiles` | Delete its files. |
| `BlobManagement.Container.CreateDirectories` | Create directories in it. |
| `BlobManagement.Container.UpdateDirectories` | Rename and move its directories. |
| `BlobManagement.Container.DeleteDirectories` | Delete its directories. |

A grant on a container is the module-wide permission limited to that container. This is how a business system's role is given its container: grant the role `BlobManagement.Container.View` and `BlobManagement.Container.UploadFiles` on it, and its users can open the container in the explorer and upload to it, without seeing any other.

## Restricted Directories

A directory can have permissions of its own. It then is **restricted** (`isRestricted`, switched by a holder of `BlobManagement.ManagePermissions`), and the grants on it count. The resource is `Dignite.Abp.BlobManagement.DirectoryDescriptor`, keyed by the directory's id:

| Permission | |
|---|---|
| `BlobManagement.Directory.View` | See the directory and everything below it. |
| `BlobManagement.Directory.CreateDirectories` | Create directories in it and below it. |
| `BlobManagement.Directory.UpdateDirectories` | Rename and move the directories in it and below it. |
| `BlobManagement.Directory.DeleteDirectories` | Delete the directories in it and below it. |

A restricted directory, with everything below it, is seen - and its subdirectories managed - only by those granted on it and by holders of the module-wide permissions. A directory that is not restricted follows the nearest restricted directory above it, or the container; so "below it" ends at the next restricted directory down, which has grants of its own. What may be done with the **files** in a restricted directory stays the container's permission. Grants on a directory are kept while it is not restricted, and count again when it is restricted again; they are deleted with the directory.

## Who May Do What

A **place** is the top of a container or a directory, and its **scope** is the nearest restricted directory at or above it, else the container. Every file and every directory at a place shares the place's answer:

| | Scope is the container | Scope is a restricted directory |
|---|---|---|
| View (list its directories and files) | `BlobManagement.Containers`, or `BlobManagement.Container.View` | `BlobManagement.Containers`, or `BlobManagement.Directory.View` |
| Upload, rename and move, delete files | View, and the module-wide or the container's permission | View, and the module-wide or the **container's** permission |
| Create, rename and move, delete subdirectories | View, and the module-wide or the container's permission | View, and the module-wide or the **directory's** permission |

Nothing is allowed at a place the caller may not view: a role granted `BlobManagement.Container.UploadFiles` also needs `BlobManagement.Container.View`. Further rules:

* **A directory** is renamed, moved and deleted by the permission of the place it is in, by a caller who may view it. A move also takes creating directories where it lands. A restricted directory keeps its restriction where it goes.
* **A file** is moved by renaming and moving where it is, and uploading where it lands.
* **The uploader of a file may delete it**, wherever it is: an application's cleanup after a form is saved relies on it.
* **Reading one file by its id** - its details or its bytes - takes a signed-in user and nothing else. The id is what a business system keeps and hands out under its own authorization; listing is what the permissions govern. Any user of the tenant who has a file's id can read it: a business system whose files must be readable only by some users stores them through the [provider](provider.md), under its own authorization, and hands out [download tickets](tickets.md). A `PublicRead` container stays anonymously readable by address.
* **One container's limits**, by its name, are read by every signed-in user (the file input needs them). The **list of containers** is those the caller holds `BlobManagement.Containers` for, or any grant on.
* **The directories at the top of a container** include each restricted directory the caller is granted on, even when the caller does not otherwise see the container, so that a directory granted there can be reached.

A token without a user is allowed none of these, and nobody is allowed any of them while the tenant's Blob Management is off ([The Tenant Feature](attaching-files.md#the-tenant-feature)).

Each request asks the grant store once for the place and once for each restricted directory it has to tell apart, never once per file. What the caller may do is sent with every container (`access`) and directory (`access`, `canUpdate`, `canDelete`), so a UI offers each action only where it is allowed.

## What It Needs

ABP's [Permission Management](https://abp.io/docs/latest/modules/permission-management) with its resource permissions, in the host that runs Blob Management; the Angular resource permission dialog comes from `@abp/ng.permission-management`. A Blob Management that serves only applications through the provider needs none of this, and runs without Permission Management. When it runs as [its own service](hosting.md#blob-management-as-its-own-service), it needs the permission data the other services share.

## See Also

* [Directories](directories.md)
* [Attaching Files](attaching-files.md)
* [Angular UI](angular-ui.md)
