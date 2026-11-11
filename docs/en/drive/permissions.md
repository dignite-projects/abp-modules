```json
//[doc-seo]
{
    "Description": "Dignite Drive permissions: Drive.Files.Upload and Drive.Files.Delete, reading a file by its id, the uploader deleting their own file, and why an application's own permissions are not consulted."
}
```

# Permissions

> Drive has not been released yet; see the [overview](index.md).

The [file API](attaching-files.md) is authorized by the Drive's own permissions, never by another module's: whoever should upload to the Drive is granted the Drive's permissions, as a role holds the permissions of every service it uses. (The [provider](provider.md) path authorizes applications, not users, and uses none of this.)

## The Permissions

In the `Drive` group of ABP's permission management:

| Permission | |
|---|---|
| `Drive.Files` | The parent of the two below; it grants nothing by itself. |
| `Drive.Files.Upload` | Upload files to every user drive, and ask for upload links. |
| `Drive.Files.Delete` | Delete every file of the user drives, not only one's own uploads. |

They are module-wide: there is no permission per drive and no grant on a single file.

## Who May Do What

| | Allowed to |
|---|---|
| List the user drives and their limits | Every signed-in user. |
| Upload a file, or ask for an upload link | A holder of `Drive.Files.Upload`. |
| Read a file's details and bytes by its id | Every signed-in user. In a `PublicRead` drive, anyone, at the file's public address. |
| Delete a file | The user who uploaded it, and a holder of `Drive.Files.Delete`. |

A token without a user is allowed none of these, and nobody is allowed any of them while the tenant's Drive is off ([The Drive Feature](attaching-files.md#the-drive-feature)).

**Reading needs a signed-in user and nothing finer.** Any user of the tenant who has a file's id can read it. A file id is a random GUID and is not listed anywhere, but it is not a secret either: an application whose files must be readable only by some users stores them through the [provider](provider.md), under its own authorization, and hands out [download tickets](tickets.md).

## Why Not the Application's Permissions

A browser uploads straight to the Drive's API, so the application cannot check its own permission first without proxying every upload. Checking the application's permissions inside the Drive would bring back the bridging to business permission names that the Drive is designed to avoid, and would fail when the Drive runs as its own service. So the rule is the Drive's: an administrator grants `Drive.Files.Upload` (and, where others' files should be deletable, `Drive.Files.Delete`) to the roles that edit content.

## What It Needs

The permissions need ABP's [Permission Management](https://abp.io/docs/latest/modules/permission-management) as usual. A Drive that serves only applications through the provider needs none of this, and runs without Permission Management. When the Drive runs as [its own service](hosting.md#the-drive-as-its-own-service), the file API needs the permission data the other services share.

## See Also

* [Attaching Files](attaching-files.md)
* [Angular UI](angular-ui.md)
