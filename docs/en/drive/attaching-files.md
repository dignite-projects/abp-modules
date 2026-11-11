```json
//[doc-seo]
{
    "Description": "Attach files with Dignite Drive: user drives, the file HTTP API that uploads, reads and deletes one file by its id, upload links for users, public reads and the Drive feature."
}
```

# Attaching Files

> Drive has not been released yet; see the [overview](index.md).

People attach files to what an application lets them edit - an image to an article, a document to a record - by uploading them to a **user drive** of the Drive. The Drive stores each file and returns it with an id; the application keeps that id (or, for a public file, its address) in its own data. A user drive shares the storage of the [provider](provider.md) - the same file descriptors, tenant quota, de-duplication and upload rules - but not its containers: applications store in **system** containers, people in **user drives**, and neither reaches the other's.

The Drive keeps no listing of a user drive: there are no folders, no search, and no call that returns a drive's files. A file is reached by its id, so whatever attaches it keeps the id.

## Declaring a User Drive

A user drive is a Drive container of `Kind = UserDrive` ([Drive Containers](drive-host.md#drive-containers)):

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    options.Containers.Configure("attachments", container =>
    {
        container.UseDriveContainer(drive => drive.Kind = DriveContainerKind.UserDrive);
        container.UseDocumentsPreset();
    });
});
````

Or in the `Drive` section of `appsettings.json`:

````json
{
  "Drive": {
    "Containers": {
      "attachments": { "Kind": "UserDrive", "Preset": "Documents" },
      "images":      { "Kind": "UserDrive", "PublicRead": true, "Preset": "Images" }
    }
  }
}
````

`AllowedClientIds` applies to system containers only; on a user drive it stops the host at startup.

## The File API

Signed-in users only: a token without a user - an application acting for itself - is refused (applications use the [provider](provider.md)). A system container, and any file in one, is not found here, whoever asks. Who may upload, read and delete is described in [Permissions](permissions.md).

| | |
|---|---|
| `GET api/drive/containers` | The user drives, with what each accepts: `maxFileSize` (bytes), `allowedFileExtensions` (empty: any type), `imageMaxWidth` and `imageMaxHeight` when it resizes pictures, and `publicRead`. |
| `GET api/drive/containers/{name}` | One of them. |
| `POST api/drive/files` | Uploads a file: a form with `containerName`, an optional `name` (the uploaded file's own name otherwise) and the file in `file`. Returns the stored file. |
| `POST api/drive/files/upload-tickets` | An [upload link](#upload-links-for-users) for one file. |
| `GET api/drive/files/{id}` | The file's details. |
| `GET api/drive/files/{id}/content` | Its bytes, always as an attachment - a user's file may be HTML, and is never rendered on the Drive's address. |
| `DELETE api/drive/files/{id}` | Deletes it. Deleting a file that does not exist succeeds. |

A file (`DriveFileDto`) has its `id`, `containerName`, `name`, `mimeType` (detected from the bytes), `size`, its creation and modification times and creator, and `url` in a public drive. Names may repeat: a file is addressed by its id only. As everywhere in the Drive, the type is detected from the bytes, the same bytes are stored once, and the tenant's [storage quota](drive-host.md#tenant-storage) applies.

`DriveFileDto` and `DriveContainerDto` are [extensible](https://abp.io/docs/latest/framework/fundamentals/object-extensions): a module that builds on the Drive and adds to its files or containers sends what it adds in `extraProperties`.

From C#, the same services are `IDriveContainerAppService` and `IDriveFileAppService` in `Dignite.Drive.Application.Contracts` - in process with `Dignite.Drive.Application`, or remote with `Dignite.Drive.HttpApi.Client`. A deleted file is kept as a deleted row.

## Upload Links for Users

A client that cannot send a file's bytes through an API call - an AI assistant, for which they would be text to write out - can ask for a link instead:

````csharp
var ticket = await _fileAppService.CreateUploadTicketAsync(new CreateDriveFileUploadTicketInput
{
    ContainerName = "attachments",
    FileName = "minutes.pdf"
});

// POST the file to ticket.Url as a form (field "content"); the answer is the stored file.
````

* Issuing is authorized exactly as the upload would be, so a link is never issued for an upload that would be refused.
* The link names the user who asked for it. Whoever posts the file to it, the upload runs **as that user**: the drive and the permission are checked again at that moment, with the roles the user had when asking. A permission revoked in between makes the link invalid (`Drive:InvalidTicket`, HTTP 403).
* It is good for one successful upload, until it expires (10 minutes by default, `LifetimeSeconds` up to an hour); a refused upload can be retried.
* It is redeemed at `POST api/drive/tickets/upload-file`, and is not a [system ticket](tickets.md): neither can be redeemed at the other's address.

The [MCP tools](mcp.md) hand these links to AI assistants.

## Public Reads

In a user drive with `PublicRead = true`, every file has a `url`: its public address, `/api/drive/public/{container}/{blob}` with `?Width=&Height=` for pictures, as for system containers ([Public Addresses](provider.md#public-addresses)). The address names the file's **physical BLOB** - 32 random hexadecimal digits - rather than its name, which need not be unique here. A file of a drive that is not public has no address; it is read through `GET api/drive/files/{id}/content`.

## The Drive Feature

The file API is part of the tenant's Drive: with the tenant's `Drive.Enable` [feature](drive-host.md#tenant-storage) off, every call is refused (ABP's feature error, HTTP 403), whatever the caller's permissions, and an upload link issued before answers `Drive:DriveDisabled` (and is not used up). Public reads are not closed by it: a file of a `PublicRead` drive stays readable at its address, while nothing new can be stored.

## Events

`FileDescriptor` publishes ABP's [automatic distributed entity events](https://abp.io/docs/latest/framework/infrastructure/event-bus/distributed) (`EntityCreatedEto<EntityEto>` and the like), so another service learns about changes without the Drive defining event types of its own.

## Errors

A missing file is ABP's not found (404); an action the caller may not take, ABP's authorization error (403). An upload link that cannot be used is `Drive:InvalidTicket` (403). The other errors are the Drive's own ([Setting Up the Drive](drive-host.md#errors)) and those of the drive's contributors, such as `Dignite.Abp.BlobStoring:ContentTooLarge`.

## See Also

* [Permissions](permissions.md)
* [Angular UI](angular-ui.md)
* [MCP Tools](mcp.md)
