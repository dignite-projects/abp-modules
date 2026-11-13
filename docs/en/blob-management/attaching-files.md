```json
//[doc-seo]
{
    "Description": "Attach files with Dignite Blob Management: the file HTTP API that uploads, lists, reads, renames, moves and deletes files by their id, upload links for users, public reads and the tenant feature."
}
```

# Attaching Files

> Blob Management has not been released yet; see the [overview](index.md).

People attach files to what a business system lets them edit - an image to an article, a document to a record - by uploading them to a managed container. Blob Management stores each file and returns it with an id; the business system keeps that id (or, for a public file, its address) in its own data. The files people upload share the storage of the [provider](provider.md) - the same file descriptors, tenant quota, de-duplication and upload rules, and the same containers.

A file a person uploads is **addressed by its id**: its name is a display name and may repeat, and it can be renamed, and moved between the [directories](directories.md) of a container that has them. A file an application saved through the provider is addressed by its name; it appears in the same listings, and is read and deleted the same way, but is not renamed or moved (`BlobManagement:FileAddressedByName`).

## The File API

Signed-in users only: a token without a user - an application acting for itself - is refused (applications use the [provider](provider.md)). A container that is not managed, and any file in one, is not found here, whoever asks. Who may do what is described in [Permissions](permissions.md).

| | |
|---|---|
| `GET api/blob-management/containers` | The containers the caller has something to do in, with what each accepts: `maxFileSize` (bytes), `allowedFileExtensions` (empty: any type), `imageMaxWidth` and `imageMaxHeight` when it resizes pictures, `publicRead`, `allowDirectories`, and `access`, what the caller may do at its top. |
| `GET api/blob-management/containers/{name}` | One container, open to every signed-in user: a file input needs its limits before it offers an upload. |
| `GET api/blob-management/files?containerName=&directoryId=` | The files of the top of a container, or of one directory, a page at a time ([Listing Files](directories.md#listing-files)). |
| `POST api/blob-management/files` | Uploads a file: a form with `containerName`, an optional `directoryId` (none: the top of the container), an optional `name` (the uploaded file's own name otherwise) and the file in `file`. Returns the stored file. |
| `POST api/blob-management/files/upload-tickets` | An [upload link](#upload-links-for-users) for one file. |
| `GET api/blob-management/files/{id}` | The file's details. |
| `GET api/blob-management/files/{id}/content` | Its bytes, always as an attachment - a file may be HTML, and is never rendered on the host's address. |
| `PUT api/blob-management/files/{id}` | Renames it: `name`. |
| `PUT api/blob-management/files/{id}/move` | Moves it to `directoryId` of the same container; none for the top ([Directories](directories.md#moving-a-file)). |
| `DELETE api/blob-management/files/{id}` | Deletes it. Deleting a file that does not exist succeeds. |

A file (`BlobManagementFileDto`) has its `id`, `containerName`, `directoryId`, `name`, `isAddressedByName`, `mimeType` (detected from the bytes), `size`, its creation and modification times and creator, and `url` in a public container. As everywhere in Blob Management, the type is detected from the bytes, the same bytes are stored once, and the tenant's [storage quota](setting-up.md#tenant-storage) applies. `mimeType` and `size` are those of what is stored: in a container on the images preset a JPEG is [stored as WebP](setting-up.md#pictures-stored-as-webp), and its `name` then ends in `.webp` (`holiday.jpg` is returned as `holiday.webp`).

`BlobManagementFileDto` and `BlobManagementContainerDto` are [extensible](https://abp.io/docs/latest/framework/fundamentals/object-extensions): what an application adds to them travels in `extraProperties`.

From C#, the same services are `IBlobManagementContainerAppService` and `IBlobManagementFileAppService` in `Dignite.Abp.BlobManagement.Application.Contracts` - in process with `Dignite.Abp.BlobManagement.Application`, or remote with `Dignite.Abp.BlobManagement.HttpApi.Client`. A deleted file is kept as a deleted row.

## Upload Links for Users

A client that cannot send a file's bytes through an API call - an AI assistant, for which they would be text to write out - can ask for a link instead:

````csharp
var ticket = await _fileAppService.CreateUploadTicketAsync(new CreateBlobManagementFileUploadTicketInput
{
    ContainerName = "attachments",
    FileName = "minutes.pdf"
});

// POST the file to ticket.Url as a form (field "content"); the answer is the stored file.
````

* Issuing is authorized exactly as the upload would be, so a link is never issued for an upload that would be refused. `DirectoryId` names the directory the file goes into; none is the top of the container.
* The link names the user who asked for it. Whoever posts the file to it, the upload runs **as that user**: the container and the permission are checked again at that moment, with the roles the user had when asking. A permission revoked in between makes the link invalid (`BlobManagement:InvalidTicket`, HTTP 403).
* It is good for one successful upload, until it expires (10 minutes by default, `LifetimeSeconds` up to an hour); a refused upload can be retried.
* It is redeemed at `POST api/blob-management/tickets/upload-file`, and is not an application's [ticket](tickets.md): neither can be redeemed at the other's address.

The [MCP tools](mcp.md) hand these links to AI assistants.

## Public Reads

In a container with `PublicRead = true`, every file has a `url`: its public address, `/api/blob-management/public/{container}/{blob}` with `?Width=&Height=` for pictures ([Public Addresses](provider.md#public-addresses)). For a file a person uploaded the address names the file's **physical BLOB** - 32 random hexadecimal digits and an extension - rather than its name, which need not be unique; renaming or moving the file leaves the address as it was. For a file an application saved, it names the file's name. A file of a container that is not public has no address; it is read through `GET api/blob-management/files/{id}/content`.

## The Tenant Feature

The container, file and directory services are part of the tenant's Blob Management: with the tenant's `BlobManagement.Enable` [feature](setting-up.md#tenant-storage) off, every call is refused (ABP's feature error, HTTP 403), whatever the caller's permissions, and an upload link issued before answers `BlobManagement:BlobManagementDisabled` (and is not used up). Public reads are not closed by it: a file of a `PublicRead` container stays readable at its address, while nothing new can be stored.

## Events

`FileDescriptor` publishes ABP's [automatic distributed entity events](https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed) (`EntityCreatedEto<EntityEto>` and the like), so another service learns about changes without Blob Management defining event types of its own.

## Errors

A missing file is ABP's not found (404); an action the caller may not take, ABP's authorization error (403). An upload link that cannot be used is `BlobManagement:InvalidTicket` (403). The other errors are Blob Management's own ([Setting Up](setting-up.md#errors), [Directories](directories.md#errors)) and those of the container's contributors, such as `Dignite.Abp.BlobStoring:ContentTooLarge`.

## See Also

* [Directories](directories.md)
* [Permissions](permissions.md)
* [Angular UI](angular-ui.md)
* [MCP Tools](mcp.md)
