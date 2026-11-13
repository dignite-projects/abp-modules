```json
//[doc-seo]
{
    "Description": "Keep files by id in Dignite Blob Management: IBlobManagementFileService, what a business system's file library calls to store, list, read, rename, move and delete files - in process or as an OAuth client - and how the uploader is recorded."
}
```

# Keeping Files by Id

> Blob Management has not been released yet; see the [overview](index.md).

A business system that keeps a file library - attachments to a record, documents, media - stores its files in a managed container **by id**: Blob Management stores each file and returns it with an id, and the business system keeps that id (or, for a public file, its address) in its own data. These files share everything with the BLOBs an application stores [by name](provider.md): the same file descriptors, tenant allocation, de-duplication, upload rules and containers.

A file kept by id has a display name, which may repeat, and can be renamed, and moved between the [directories](directories.md) of a container that has them. A file an application saved by name appears in the same listings and is read and deleted the same way, but is not renamed or moved (`BlobManagement:FileAddressedByName`).

## What the Business System Does

Blob Management has no upload of its own, and authorizes no user for files. The business system:

* **builds the UI** its users store, rename, move and delete files with;
* **applies its own rules** - who may attach what to which record - before it calls;
* **receives the upload** at an endpoint of its own and stores the bytes through the file service, and **hands out the bytes** of a file that is not public the same way: a browser does not reach Blob Management for them;
* **calls as an application**: in the same process as a trusted caller, or remotely as an OAuth client the container allows.

A file that no business record refers to has no value: delete a file when the record that refers to it lets go of it.

## The File Service

`IBlobManagementFileService`, in `Dignite.Abp.BlobManagement.Application.Contracts`, works in the current tenant:

| Method | |
|---|---|
| `GetAsync(id)` | One file's details. |
| `GetListAsync(input)` | The files of the top of a container, or of one directory, a page at a time. |
| `CreateAsync(input)` | Stores a file at the top of a container, or in a directory. |
| `UpdateAsync(id, input)` | Renames it. |
| `MoveAsync(id, input)` | Moves it to another directory of its container, or to the top. |
| `DeleteAsync(id)` | Deletes it. Deleting a file that is not there succeeds. |
| `GetContentAsync(id)` | Its bytes, in any container, public or not. |

The host decides which implementation answers, as for the [provider](provider.md):

* **In the same process**: the host installs `Dignite.Abp.BlobManagement.Application`. The service is a trusted caller and checks no permission.
* **Remote**: the host installs `Dignite.Abp.BlobManagement.HttpApi.Client` and its [client-credentials configuration](provider.md#a-remote-blob-management). The service calls the file API as the OAuth client, which must be in the container's `AllowedClientIds`.

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

A file (`BlobManagementFileDto`) has its `id`, `containerName`, `directoryId`, `name`, `isAddressedByName`, `mimeType` (detected from the bytes), `size`, `creatorId`, `clientId`, its creation and modification times, and `url` in a public container. `mimeType` and `size` are those of what is stored: in a container on the images preset a JPEG is [stored as WebP](setting-up.md#pictures-stored-as-webp), and its `name` then ends in `.webp`. `BlobManagementFileDto` is [extensible](https://abp.io/docs/latest/framework/fundamentals/object-extensions): what an application adds to it travels in `extraProperties`. A deleted file is kept as a deleted row.

## The Uploader

`CreatorId` is the user a file is **counted for** in the [usage figures](usage.md), and nothing else: no right comes with it.

* In the same process, it is the application's current user, as ABP's auditing records it.
* Remotely, the client's token carries no user, so `RemoteBlobManagementFileService` - what `Dignite.Abp.BlobManagement.HttpApi.Client` registers - fills `CreatorId` with the application's current user. Blob Management believes it because it believes the client.
* `CreateBlobManagementFileInput.CreatorId` names another user, when the application stores for someone else.

A file stored over HTTP also records the client that stored it (`clientId`).

## The File API

The file service over HTTP is `IBlobManagementFileAppService`, at `api/blob-management/files`. It admits two callers:

* **an OAuth client the file's container allows**, with a token that carries no user - for everything. A file in a container the client may not use is not found;
* **a user who holds `BlobManagement.Containers`** - for the reads only: the listing, one file and its bytes. This is what the [management page](angular-ui.md#the-management-page) reads with.

No user stores, renames, moves or deletes a file here.

| | |
|---|---|
| `GET api/blob-management/files?containerName=&directoryId=` | The files of a place, a page at a time. |
| `GET api/blob-management/files/{id}` | One file. |
| `GET api/blob-management/files/{id}/content` | Its bytes, always as an attachment - a file may be HTML, and is never rendered on the host's address. |
| `POST api/blob-management/files` | Stores a file: a form with `containerName`, an optional `directoryId`, `name` and `creatorId`, and the file in `file`. |
| `PUT api/blob-management/files/{id}` | Renames it: `name`. |
| `PUT api/blob-management/files/{id}/move` | Moves it to `directoryId` of the same container; none for the top. |
| `DELETE api/blob-management/files/{id}` | Deletes it. |

## Public Reads

In a container with `PublicRead = true`, every file has a `url`: its public address, `/api/blob-management/public/{container}/{blob}` with `?Width=&Height=` for pictures ([Public Addresses](provider.md#public-addresses)). For a file kept by id the address names the file's **physical BLOB** - 32 random hexadecimal digits and an extension - rather than its name, which need not be unique; renaming or moving the file leaves the address as it was. A file of a container that is not public has no address: the application reads it with `GetContentAsync` and hands the bytes on.

## The Tenant Feature

The file, directory, container and usage services are part of the tenant's Blob Management: with the tenant's `BlobManagement.Enable` [feature](setting-up.md#tenant-storage) off, every call is refused (ABP's feature error, HTTP 403). Public reads are not closed by it: a file of a `PublicRead` container stays readable at its address, while nothing new can be stored.

## Events

`FileDescriptor` publishes ABP's [automatic distributed entity events](https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed) (`EntityCreatedEto<EntityEto>` and the like), so another service learns about changes without Blob Management defining event types of its own.

## Errors

A missing file is ABP's not found (404); a caller the file API does not admit, ABP's authorization error (403). The other errors are Blob Management's own ([Setting Up](setting-up.md#errors), [Directories](directories.md#errors)) and those of the container's contributors, such as `Dignite.Abp.BlobStoring:ContentTooLarge`.

## See Also

* [Using It From an Application](provider.md)
* [Directories](directories.md)
* [Usage](usage.md)
* [Permissions](permissions.md)
