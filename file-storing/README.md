# Dignite Abp File Storing

> Part of [**dignite-projects/abp-modules**](https://github.com/dignite-projects/abp-modules) — see
> the [repository README](../README.md) for the other modules, and
> [CONTRIBUTING.md](../CONTRIBUTING.md) for the build, versioning, and release process shared across
> them. Formerly developed at `dignite-projects/abp-file-storing`; **no package ID changed** in the
> move.

A thin enhancement layer on [ABP BlobStoring](https://abp.io/docs/latest/framework/infrastructure/blob-storing):
per-container upload rules, and one service that applies them and stores the result.

- `core/src/Dignite.Abp.FileStoring` — the `IFileHandler` pipeline (`FileSizeLimitHandler`,
  `FileTypeCheckHandler`), `IFileStorer`, content-based MIME detection.
- `core/src/Dignite.Abp.FileStoring.Imaging` — `ImageResizeHandler`, an optional upload-time resize.

There is no metadata layer, API or UI here. An application keeps its own file records and authorization and
uses `IFileStorer` to put bytes into a container. (File Explorer, the DDD file browser that used to ship from
this folder, moved to the `site` repository as part of Site in `10.0.0-rc.25` — see
[`docs/core-only-decision.md`](docs/core-only-decision.md).)

## Usage

Depend on `DigniteAbpFileStoringModule` (or `DigniteAbpFileStoringImagingModule`) and configure a container:

```csharp
Configure<AbpBlobStoringOptions>(options =>
{
    options.Containers.Configure<AttachmentsContainer>(container =>
    {
        container.UseFileSystem(fs => fs.BasePath = "...");
        container.AddFileSizeLimitHandler(h => h.MaxFileSize = 20); // MB
        container.AddFileTypeCheckHandler(h => h.AllowedFileTypeNames = [".pdf", ".png", ".jpg"]);
    });
});
```

Store an upload:

```csharp
var stored = await _fileStorer.StoreAsync<AttachmentsContainer>(file.FileName, file.GetStream(), cancellationToken);

try
{
    await _attachmentRepository.InsertAsync(
        new Attachment(GuidGenerator.Create(), ticketId, stored.BlobName, file.FileName, stored.MimeType, stored.Size, stored.Hash),
        autoSave: true,
        cancellationToken);
}
catch
{
    await _fileStorer.DeleteAsync<AttachmentsContainer>(stored.BlobName);
    throw;
}
```

What `StoreAsync` guarantees:

- **The size limit binds while reading.** The upload is copied into a buffer that is never allowed past the
  container's `AddFileSizeLimitHandler` limit (100 MB, `FileConsts.DefaultMaxFileSizeInBytes`, when none is set).
  Still enforce a request-body limit at the HTTP layer.
- **The MIME type comes from the content.** `IMimeTypeDetector` reads the file's signature; a file whose content
  contradicts its extension is rejected (`Dignite.Abp.File:0005`). The API does not accept a MIME type from the
  caller.
- **Handlers run in the configured order** on the buffered content; `StoredFileInfo` describes what was stored
  after them (a resized image reports its resized size and hash).
- **No partial blobs.** A save that fails after writing is followed by a delete of that blob. A name collision
  (`BlobAlreadyExistsException`, only possible with a custom `IBlobNameGenerator`) is never "compensated" by deleting
  the existing blob.

Dedup by content is up to you: compare `StoredFileInfo.Hash` (SHA-256, upper-case hex) with your own records.
