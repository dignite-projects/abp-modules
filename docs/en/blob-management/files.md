```json
//[doc-seo]
{
    "Description": "Keep files by id in Dignite Blob Management: IBlobManagementFileService, what a business system's file library calls - in process or as an OAuth client - public files, upload tickets and read links for its browser, and how the uploader is recorded."
}
```

# Keeping Files by Id

> Blob Management has not been released yet; see the [overview](index.md).

A business system that keeps a file library - attachments to a record, documents, media - stores its files in a managed container **by id**: Blob Management stores each file and returns it with an id, and the business system keeps that id (or, for a public file, its address) in its own data. These files share everything with the BLOBs an application stores [by name](provider.md): the same file descriptors, tenant allocation, de-duplication, upload rules and containers.

A file kept by id has a display name, which may repeat, and can be renamed, and moved between the [directories](directories.md) of a container that has them. A file an application saved by name appears in the same listings and is read and deleted the same way, but is not renamed or moved (`BlobManagement:FileAddressedByName`).

## What the Business System Does

Blob Management has no business rules, and authorizes no user for files. The business system:

* **builds the UI** its users store, rename, move and delete files with;
* **applies its own rules** - who may attach what to which record, who may see a file - before it calls;
* **calls as an application** from its back end: in the same process as a trusted caller, or remotely as an OAuth client the container allows. That client's token reaches every tenant, so it never leaves the back end;
* **lets its browser in** with what it asks Blob Management for on the user's behalf: an [upload ticket](#upload-tickets) to send a file, a [read link](#read-links) to read one that is not public - or it receives the upload and hands the bytes on itself;
* **marks a file public** when anyone may read it ([Public Files](#public-files)).

A file that no business record refers to has no value: delete a file when the record that refers to it lets go of it.

## The File Service

`IBlobManagementFileService`, in `Dignite.Abp.BlobManagement.Application.Contracts`, works in the current tenant:

| Method | |
|---|---|
| `GetAsync(id)` | One file's details - also of a file still [pending](#upload-tickets). |
| `GetListAsync(input)` | The files of the top of a container, or of one directory, a page at a time. Pending files are left out. |
| `CreateAsync(input)` | Stores a file at the top of a container, or in a directory. |
| `UpdateAsync(id, input)` | Renames it. |
| `MoveAsync(id, input)` | Moves it to another directory of its container, or to the top. |
| `SetPublicAsync(id, input)` | Marks it public or not ([Public Files](#public-files)). |
| `CreateUploadTicketAsync(input)` | An [upload ticket](#upload-tickets) for the browser. |
| `ConfirmAsync(id)` | Confirms a file uploaded with a ticket. |
| `CreateReadLinkAsync(id, input)`, `CreateReadLinksAsync(input)` | [Read links](#read-links) for the browser. |
| `DeleteAsync(id)` | Deletes it. Deleting a file that is not there succeeds. |
| `GetContentAsync(id)` | Its bytes, public or not. |

The host decides which implementation answers, as for the [provider](provider.md):

* **In the same process**: the host installs `Dignite.Abp.BlobManagement.Application`. The service is a trusted caller and checks no permission.
* **Remote**: the host installs `Dignite.Abp.BlobManagement.HttpApi.Client` and its [client-credentials configuration](provider.md#a-remote-blob-management). The service calls the file API as the OAuth client, which must be granted the container's files ([Granting an Application](permissions.md#granting-an-application)).

````csharp
public class AttachmentManager : ITransientDependency
{
    private readonly IBlobManagementFileService _files;

    public AttachmentManager(IBlobManagementFileService files)
    {
        _files = files;
    }

    public async Task<Guid> AttachAsync(IRemoteStreamContent upload, Guid? directoryId)
    {
        // The business system's own rules have run before this.
        var file = await _files.CreateAsync(new CreateBlobManagementFileInput
        {
            ContainerName = "attachments",
            DirectoryId = directoryId,   // null: the top of the container
            File = upload                // its file name is the file's name unless Name is set
        });

        return file.Id;
    }
}
````

`GetListAsync` takes `ContainerName`, `DirectoryId` (none for the top of the container), `Filter` (a part of the name, whatever its case), `Sorting` (`Name`, `Size`, `MimeType`, `CreationTime` or `LastModificationTime`, then `asc` or `desc`; default `Name asc`; anything else is `BlobManagement:SortingNotSupported`) and ABP's paging. In a container without directories the same call, without `DirectoryId`, lists the whole container.

A file (`BlobManagementFileDto`) has its `id`, `containerName`, `directoryId`, `name`, `isAddressedByName`, `mimeType` (detected from the bytes), `size`, `creatorId`, `clientId`, `isPublic`, `isPending`, its creation and modification times, and `url` while it is public. `mimeType` and `size` are those of what is stored: in a container on the images preset a JPEG is [stored as WebP](setting-up.md#pictures-stored-as-webp), and its `name` then ends in `.webp`. `BlobManagementFileDto` is [extensible](https://abp.io/docs/latest/framework/fundamentals/object-extensions): what an application adds to it travels in `extraProperties`. A deleted file is kept as a deleted row.

## The Uploader

`CreatorId` is the user a file is **counted for** in the [usage figures](usage.md), and nothing else: no right comes with it.

* In the same process, it is the application's current user, as ABP's auditing records it.
* Remotely, the client's token carries no user, so `RemoteBlobManagementFileService` - what `Dignite.Abp.BlobManagement.HttpApi.Client` registers - fills `CreatorId` with the application's current user. Blob Management believes it because it believes the client.
* `CreateBlobManagementFileInput.CreatorId` names another user, when the application stores for someone else.
* A file uploaded with a ticket is counted for the user the ticket was issued for.

A file stored over HTTP also records the client that stored it (`clientId`).

## Public Files

Whether anyone may read a file at its public address, without signing in, is the business system's call, kept on the file (`isPublic`):

* `CreateBlobManagementFileInput.IsPublic` marks a file when it is stored, and `SetPublicAsync(id, new SetBlobManagementFilePublicInput { IsPublic = … })` changes it later.
* A BLOB an application saves by name takes it from the application's container ([Public Addresses](provider.md#public-addresses)), at each save; `SetPublicAsync` does not change it (`BlobManagement:FileAddressedByName`).

A public file has a `url`: `/blobs/{container}/{blob}`, with `?Width=&Height=` for pictures ([Public Addresses](provider.md#public-addresses)). For a file kept by id the address names the file's **physical BLOB** - 32 random hexadecimal digits and an extension - rather than its name, which need not be unique; renaming or moving the file leaves the address as it was.

Files kept by id that hold the same bytes share one physical BLOB, and so **one address**: it answers while one of them is public - marking one file private does not close an address another public file has, and a file that is not public reads at it while one with the same bytes is public. A download from it is named after the file only while a single public file has the BLOB, after the BLOB otherwise. A file that is not public, or not confirmed yet, has no `url`, and its address answers 404 unless another file opens it. A cache that kept a response keeps it for up to a day after the file stops being public.

## Upload Tickets

A ticket lets a browser send one file straight to Blob Management, without passing through the business system and without signing in to it. The business system asks for it on its back end, for its current user:

````csharp
var ticket = await _files.CreateUploadTicketAsync(new CreateBlobManagementUploadTicketInput
{
    ContainerName = "attachments",
    DirectoryId = directoryId,                      // optional
    MaxSize = 10 * 1024 * 1024,                     // optional: at most the container's own limit
    AllowedContentTypes = { "application/pdf", "image/*" }, // optional: detected from the bytes
    LifetimeInSeconds = 300                         // optional
});
// ticket.UploadUrl: /api/blob-management/uploads/{ticket}
// ticket.FileId:    the id the file will have
````

* The ticket is signed by Blob Management with ASP.NET Core Data Protection and carries the tenant, the container, the directory, the file's id, the user (`UploaderId`; by default the current user, which the remote service sends), the client, the size and the types. The browser can change none of it.
* Its lifetime is `LifetimeInSeconds`, else `BlobManagementUploadOptions.TicketLifetime` (5 minutes), and never more than `MaxTicketLifetime` (1 hour).
* The browser posts the file to `UploadUrl` - relative to Blob Management's address - as the multipart field `file` (or the raw body, with `?name=`). The request is anonymous: no token, cookie, antiforgery token or tenant header is needed or used. The body is limited to the ticket's size.

````javascript
const form = new FormData();
form.append('file', input.files[0]);
const response = await fetch(blobManagementUrl + ticket.uploadUrl, { method: 'POST', body: form });
const file = await response.json(); // file.id === ticket.fileId, file.isPending === true
````

A ticket stores **one file, once**: the file takes the ticket's id, and a second upload is refused (`BlobManagement:UploadTicketUsed`, 409). An expired or altered ticket is `BlobManagement:UploadTicketInvalid` (403). The container's own rules apply as for any file.

The file is **pending** until the business system confirms it: it is not listed, gets no read link and is not read publicly, but it counts in the usage and the allocation. When the browser reports the upload, the business system reads the file by its id, checks that `creatorId` is its current user (and whatever else it requires), files it with its record and confirms it:

````csharp
var file = await _files.GetAsync(fileId);
if (!file.IsPending || file.CreatorId != CurrentUser.Id)
{
    throw new AbpAuthorizationException();
}

await _files.ConfirmAsync(fileId);
````

A file still pending after `BlobManagementUploadOptions.PendingLifetime` (24 hours) is deleted by a background worker that runs every `CleanupPeriod` (1 hour), in every tenant; with several instances, one of them at a time, under the [distributed lock](hosting.md#blob-management-as-its-own-service). A host that turns ABP's background workers off runs `BlobManagementPendingFileCleaner.CleanAsync` itself.

````csharp
Configure<BlobManagementUploadOptions>(options =>
{
    options.TicketLifetime = TimeSpan.FromMinutes(10);
    options.PendingLifetime = TimeSpan.FromHours(6);
});
````

## Read Links

A read link lets a browser read a file that is not public, for a few minutes, straight from Blob Management. The business system asks for it once it has decided its user may see the file:

````csharp
var link = await _files.CreateReadLinkAsync(fileId, new CreateBlobManagementReadLinkInput());
// link.Url: /api/blob-management/links/{link}, link.ExpirationTime

var page = await _files.CreateReadLinksAsync(new CreateBlobManagementReadLinksInput { Ids = fileIds });
````

* A link names the tenant and the file, signed as a ticket is. Its lifetime is `LifetimeInSeconds`, else `BlobManagementReadLinkOptions.LinkLifetime` (10 minutes), never more than `MaxLinkLifetime` (1 hour).
* **The same file within one window is the same link**: the expiration is rounded up to `LinkWindow` (5 minutes), and the link is kept in the distributed cache until then, so a browser's cache - keyed by the address - serves the file again rather than reading it anew. A cache that misses only costs a new link.
* `CreateReadLinksAsync` takes up to 100 ids; a file that is not there, not the client's, or pending is left out of the result.
* A pending file gets no link (`BlobManagement:FilePending`, 409).

The browser reads `GET` (or `HEAD`) `/api/blob-management/links/{link}` anonymously. The response is `Cache-Control: private`, for no longer than the link has left, with an `ETag` (a matching `If-None-Match` is answered `304`), `nosniff`, and a type outside the module's list served as an attachment. An expired or altered link answers 403; a file deleted meanwhile, 404. A link is a credential: give it to the user it is for, and do not log it.

````csharp
Configure<BlobManagementReadLinkOptions>(options =>
{
    options.LinkLifetime = TimeSpan.FromMinutes(30);
    options.LinkWindow = TimeSpan.FromMinutes(10);
});
````

## The File API

The file service over HTTP is `IBlobManagementFileAppService`, at `api/blob-management/files`. It admits two callers:

* **an OAuth client granted the files of the file's container**, with a token that carries no user - for everything. A file in a container the client is not granted is not found;
* **a user who holds `BlobManagement.Containers`** - for the reads only: the listing, one file and its bytes. This is what the [management page](angular-ui.md#the-management-page) reads with.

No user stores, renames, moves or deletes a file here, and no user gets a ticket or a link.

| | |
|---|---|
| `GET api/blob-management/files?containerName=&directoryId=` | The files of a place, a page at a time. |
| `GET api/blob-management/files/{id}` | One file. |
| `GET api/blob-management/files/{id}/content` | Its bytes, always as an attachment - a file may be HTML, and is never rendered on the host's address. |
| `POST api/blob-management/files` | Stores a file: a form with `containerName`, an optional `directoryId`, `name`, `creatorId` and `isPublic`, and the file in `file`. |
| `PUT api/blob-management/files/{id}` | Renames it: `name`. |
| `PUT api/blob-management/files/{id}/move` | Moves it to `directoryId` of the same container; none for the top. |
| `PUT api/blob-management/files/{id}/public` | Marks it public or not: `isPublic`. |
| `POST api/blob-management/files/upload-tickets` | An upload ticket. |
| `POST api/blob-management/files/{id}/confirm` | Confirms a file uploaded with a ticket. |
| `POST api/blob-management/files/{id}/read-link`, `POST api/blob-management/files/read-links` | Read links. |
| `DELETE api/blob-management/files/{id}` | Deletes it. |

The browser's two addresses are not part of the API, and have no client proxy:

| | |
|---|---|
| `POST api/blob-management/uploads/{ticket}` | The upload with a ticket. |
| `GET`/`HEAD api/blob-management/links/{link}` | The read with a link. |

## The Tenant Feature

The file, directory, container and usage services are part of the tenant's Blob Management: with the tenant's `BlobManagement.Enable` [feature](setting-up.md#tenant-storage) off, every call is refused (ABP's feature error, HTTP 403), and an upload with a ticket is refused too. Reads are not closed by it: a public file stays readable at its address, and a read link handed out stays readable until it expires, while nothing new can be stored.

## Events

`FileDescriptor` publishes ABP's [automatic distributed entity events](https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed) (`EntityCreatedEto<EntityEto>` and the like), so another service learns about changes without Blob Management defining event types of its own.

## Errors

A missing file is ABP's not found (404); a caller the file API does not admit, ABP's authorization error (403). The other errors are Blob Management's own ([Setting Up](setting-up.md#errors), [Directories](directories.md#errors)) and those of the container's contributors, such as `Dignite.Abp.BlobStoring:ContentTooLarge`.

## See Also

* [Using It From an Application](provider.md)
* [Directories](directories.md)
* [Usage](usage.md)
* [Permissions](permissions.md)
* [Hosting Modes](hosting.md)
