```json
//[doc-seo]
{
    "Description": "Reject BLOBs larger than a limit in an ABP BLOB container, without buffering more than the limit, with the MaxSize pipeline contributor."
}
```

# Size Limit

The **size limit** contributor rejects content larger than a limit with the `Dignite.Abp.BlobStoring:ContentTooLarge` error code. It is part of the `Dignite.Abp.BlobStoring.Pipeline` package; see the [overview](index.md) for the installation.

## Configuration

Use the `AddMaxSizeContributor` extension method on a container configuration:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    options.Containers.Configure<AttachmentContainer>(container =>
    {
        container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 20 * 1024 * 1024); // 20 MB
    });
});
````

`MaxSizeInBytes` (`long`) is the limit, in bytes. It has no default, must be greater than `0`, and can be at most `Array.MaxLength` (about 2 GB), because the content is held in memory.

> Add the size limit **first** in every container that takes uploads. See [The Recommended Order](index.md#the-recommended-order).

## How It Works

While **saving**, the limit binds while the content is read:

* A seekable stream whose remaining length already exceeds the limit is rejected **without being read**.
* Any other stream is copied into memory chunk by chunk, and rejected as soon as the copy passes the limit. Nothing beyond the limit is buffered, and nothing is saved when it throws.

The copy then replaces the content stream, so every contributor after it works on a **seekable stream with a known length**, and does not need to copy the content again. Storage providers that need the object size before uploading also get it this way.

While **reading**, the size limit does nothing.

The size limit does not change the content, so it can be added to or removed from a container that already has BLOBs.

## It Is Not the Only Cap

Every contributor of this module that reads the content enforces a cap of its own, whether or not the size limit is configured: the allowed content types, the image contributors and GZip all stop at the container's `MaxSizeInBytes`, or at `BlobStoringPipelineConsts.DefaultMaxBufferedBytes` (100 MB) when the container has no size limit. So content is never buffered without a bound.

`DefaultMaxBufferedBytes` is a static property; change it once at application startup if 100 MB does not fit your application. Configuring the size limit is still preferable, because it also makes the stream seekable for the contributors after it.

> A size limit in the BLOB container does not replace a **request body limit** at the HTTP layer. Configure one in ASP.NET Core (for example `[RequestSizeLimit]` or Kestrel's `MaxRequestBodySize`), so an oversized upload is cut off before it reaches your code at all.

## See Also

* [Allowed Content Types](allowed-content-types.md)
* [Saving Uploaded Files](saving-uploaded-files.md)
