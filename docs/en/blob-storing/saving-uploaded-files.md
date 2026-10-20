```json
//[doc-seo]
{
    "Description": "Save uploaded files to an ABP BLOB container with pipeline contributors: naming the BLOB, recording its metadata, and cleaning up when a write fails."
}
```

# Saving Uploaded Files

The contributors of this module run **inside** `IBlobContainer`, so saving an upload is an ordinary `SaveAsync` call. What the module deliberately does not do is everything around it: your application names the BLOB, records what it needs about the file, and cleans up when its own write fails. This guide shows a complete upload that does all of that.

## A Complete Upload

The following [domain service](https://abp.io/docs/latest/framework/architecture/domain-driven-design/domain-services) saves an attachment of a ticket. The container is configured with a [size limit](max-size.md) of 20 MB and the [allowed content types](allowed-content-types.md) it accepts; the metadata goes to an `Attachment` [entity](https://abp.io/docs/latest/framework/architecture/domain-driven-design/entities) of the application:

````csharp
public class AttachmentManager : DomainService
{
    // The same limit as the size limit contributor of the container
    private const long MaxUploadBytes = 20 * 1024 * 1024;

    private readonly IBlobContainer<AttachmentContainer> _container;
    private readonly IRepository<Attachment, Guid> _attachmentRepository;
    private readonly IMimeTypeDetector _mimeTypeDetector;

    public AttachmentManager(
        IBlobContainer<AttachmentContainer> container,
        IRepository<Attachment, Guid> attachmentRepository,
        IMimeTypeDetector mimeTypeDetector)
    {
        _container = container;
        _attachmentRepository = attachmentRepository;
        _mimeTypeDetector = mimeTypeDetector;
    }

    public async Task<Attachment> UploadAsync(
        Guid ticketId,
        string fileName,
        Stream upload,
        CancellationToken cancellationToken = default)
    {
        // One capped, seekable copy: detecting the type and hashing both read it
        await using var content = await BlobStreamBuffering.CopyToBufferAsync(
            upload, MaxUploadBytes, cancellationToken);

        // Throws ContentTypeMismatch for a disguised file
        var mimeType = await _mimeTypeDetector.DetectAsync(content, fileName, cancellationToken);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(content, cancellationToken));
        content.Position = 0;

        // The application names the BLOB
        var blobName = GuidGenerator.Create().ToString("N");

        // The contributors of the container run here
        await _container.SaveAsync(blobName, content, overrideExisting: false, cancellationToken);

        try
        {
            return await _attachmentRepository.InsertAsync(
                new Attachment(GuidGenerator.Create(), ticketId, blobName, fileName, mimeType, content.Length, hash),
                autoSave: true,
                cancellationToken: cancellationToken);
        }
        catch
        {
            // The request may have been cancelled; the cleanup must not be
            await _container.DeleteAsync(blobName, CancellationToken.None);
            throw;
        }
    }
}
````

In an [application service](https://abp.io/docs/latest/framework/architecture/domain-driven-design/application-services), the upload usually arrives as an [`IRemoteStreamContent`](https://abp.io/docs/latest/framework/architecture/domain-driven-design/application-services#working-with-streams); pass its `GetStream()` and `FileName` to the method above.

## What to Notice

* **`BlobStreamBuffering.CopyToBufferAsync`** makes a seekable copy of a request stream that is not seekable, and fails with `ContentTooLarge` as soon as it passes the limit. Use the same limit as the container's size limit contributor.
* **`overrideExisting: false`** turns a name collision into a `BlobAlreadyExistsException` instead of a silent overwrite. Never "compensate" that by deleting the BLOB: it belongs to someone else.
* **Store the BLOB first, write the row second, and delete the BLOB if the row fails.** That is all an in-request file write needs; do not reach for an outbox to make it atomic.
* **The type, hash and size above describe the upload.** If a resize, a compression or GZip changes the stored bytes, they no longer describe what is stored. A row that needs the stored size or hash reads the BLOB back with `GetAsync`; the image contributors store ordinary images, and GZip is undone on read.
* **The BLOB name has no extension here**, so the allowed content types contributor cannot reconcile it with the content; calling `IMimeTypeDetector` with the original file name does that instead. Name the BLOB with its real extension if you want the contributors to check it themselves.

## What Stays in Your Application

* Who may read or delete a BLOB, through your own [authorization](https://abp.io/docs/latest/framework/fundamentals/authorization).
* Deduplication by content, and tenant scoping beyond what BLOB Storing itself does.
* Serving the content safely: send user content with `X-Content-Type-Options: nosniff` and, for anything that is not meant to render inline, `Content-Disposition: attachment`, ideally from a separate origin.
* A request body limit at the HTTP layer, so an oversized upload is cut off before it reaches your code.

## See Also

* [MIME Type Detection](mime-type-detection.md)
* [BLOB Storing](https://abp.io/docs/latest/framework/infrastructure/blob-storing)
