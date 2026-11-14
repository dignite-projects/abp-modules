```json
//[doc-seo]
{
    "Description": "Dignite Blob Management permissions: the management permission BlobManagement.Containers, the directory permissions granted on a container, and the grant of a container's files to an application's OAuth client."
}
```

# Permissions

> Blob Management has not been released yet; see the [overview](index.md).

Blob Management's permissions authorize **users** for the two things a user does here - read what the tenant stores (the [management page](angular-ui.md#the-management-page), the [usage](usage.md)) and keep the [directories](directories.md) of a container - and **applications** for the one thing an application does: keep the files of a container.

Files are an application's. An application that calls from another process, through the [provider](provider.md) or the [file service](files.md), is an OAuth client granted the container's files; in the same process it is trusted. Its own rules run before it calls. No permission lets a user store, rename, move or delete a file: a user's browser stores or reads one only with an [upload ticket or a read link](files.md#upload-tickets) the application asked for.

Reading is **module-wide**: one permission, granted to roles and users in ABP's permission dialog. Everything else is **granted on a container**, and nowhere else: no permission keeps the directories or the files of every container.

## Module-Wide Permissions

In the `BlobManagement` group of ABP's permission management:

| Permission | |
|---|---|
| `BlobManagement.Containers` | The management permission: enter the management page and read everything there - the usage, every container, its directories, its files and their bytes. It changes nothing. |
| `BlobManagement.ManagePermissions` | Manage the grants on containers. An administrator's permission: whoever holds it can grant themselves anything. |

## Grants on a Container

A [resource permission](https://abp.io/docs/latest/modules/permission-management) is granted on **one** container - to a role, a user or a client, in ABP's resource permission dialog - by a holder of `BlobManagement.ManagePermissions`. The resource is `Dignite.Abp.BlobManagement.BlobManagementContainer`, keyed by the container's name:

| Permission | |
|---|---|
| `BlobManagement.Container.Files` | For an application's OAuth client: read, store, rename, move, mark public and delete the container's files through the BLOB API and the file API, and ask for upload tickets and read links ([Granting an Application](#granting-an-application)). Offered on the host side only. |
| `BlobManagement.Container.View` | See the container's directory tree. The three directory permissions count only with it, or with the management permission. |
| `BlobManagement.Container.CreateDirectories` | Create directories in it. |
| `BlobManagement.Container.UpdateDirectories` | Rename and move its directories. |
| `BlobManagement.Container.DeleteDirectories` | Delete its (empty) directories. |

A role keeps the directories of a container by the grants above alone: `BlobManagement.Container.View` and the directory permissions it needs, on that container. A holder of the management permission sees every tree already, and needs the directory permissions granted on a container like anyone else - a container is added to the host's configuration, and is granted on once.

## Who May Do What

| | Allowed with |
|---|---|
| Read the usage, and list and read files | `BlobManagement.Containers` |
| See a container's directory tree | `BlobManagement.Containers`, or `BlobManagement.Container.View` on it |
| Create, rename and move, delete directories | Seeing the tree, and the permission for it granted on the container |
| Read, store, rename, move, mark public and delete files over HTTP; ask for upload tickets and read links | An OAuth client with a token that carries no user, granted `BlobManagement.Container.Files` on the container |
| Upload one file, or read one file | Anyone holding the ticket or the link an OAuth client asked for |
| Manage the grants on a container | `BlobManagement.ManagePermissions` |

* The answer is the **container's**, the same for every directory in it: no directory has permissions of its own, and no file has any.
* The **list of containers** is those the caller holds `BlobManagement.Containers` for, or any grant on. **One container's limits**, by its name, are read by every signed-in user.
* A token without a user is allowed none of the user's rows, and a token with one - whichever client it came from - none of the application's. Nobody is allowed any of them while the tenant's Blob Management is off ([The Tenant Feature](files.md#the-tenant-feature)).

Each request asks the grant store once for each container it answers about, never once per directory or file. What the caller may do with a container's directories is sent with every container as `access`.

## Granting an Application

An application that calls Blob Management from another process authenticates as an OAuth client of your authorization server ([A Remote Blob Management](provider.md#a-remote-blob-management)). Grant that client `BlobManagement.Container.Files` on each container it uses; a container granted to no client is used in process only. A grant to a client is the deployment's, not a tenant's - a client is - so `BlobManagement.Container.Files` is offered on the host side only, and a grant holds in every tenant. The client then reaches every tenant's files in that container, naming the tenant of each call: its token belongs in the application's back end and nowhere else.

* **In ABP's resource permission dialog**, the container's *Permissions* on the [management page](angular-ui.md#the-management-page), signed in on the host side: choose *Application* (the client provider), find the client and tick *Files*. A tenant's administrator does not see *Files* in the dialog. The dialog offers clients where ABP's `Volo.Abp.PermissionManagement.Domain.OpenIddict` is installed next to the OpenIddict domain (the open-source `Volo.Abp.OpenIddict.Domain`, which looks clients up) - the authorization server's host, or a Blob Management installed in it.
* **In a data seed contributor**, for a deployment that should start with its grants:

````csharp
public class BlobManagementClientsDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IResourcePermissionManager _resourcePermissionManager;

    public BlobManagementClientsDataSeedContributor(IResourcePermissionManager resourcePermissionManager)
    {
        _resourcePermissionManager = resourcePermissionManager;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (context.TenantId != null)
        {
            return; // a client's grants are the host's
        }

        await _resourcePermissionManager.SetForClientAsync(
            BlobManagementResourceNames.Container, "media", "site-app",
            BlobManagementPermissions.Container.Files, isGranted: true);
    }
}
````

`SetForClientAsync` comes with `Volo.Abp.PermissionManagement.Domain.OpenIddict` as well.

Granted to a user or a role - the dialog saves every permission of the container for whoever it edits, `Files` among them on the host side - `BlobManagement.Container.Files` lets nothing through: the BLOB and file APIs refuse a token that carries a user, whichever client issued it, before they read a grant.

## What It Needs

ABP's [Permission Management](https://abp.io/docs/latest/modules/permission-management) with its resource permissions, in the host that runs Blob Management - for the grants to applications as much as for users; the Angular resource permission dialog comes from `@abp/ng.permission-management`. A Blob Management used only in the application's own process needs none of this: its callers are trusted. When it runs as [its own service](hosting.md#blob-management-as-its-own-service), it needs the permission data the other services share.

## See Also

* [Directories](directories.md)
* [Usage](usage.md)
* [Angular UI](angular-ui.md)
