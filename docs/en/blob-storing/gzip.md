```json
//[doc-seo]
{
    "Description": "Store BLOBs GZip-compressed in an ABP BLOB container and decompress them transparently on read, with the GZip pipeline contributor."
}
```

# GZip Compression

The **GZip** contributor stores the content GZip-compressed and decompresses it when the BLOB is read. Your application keeps saving and reading the plain content through `IBlobContainer`. It is part of the `Dignite.Abp.BlobStoring.Pipeline` package; see the [overview](index.md) for the installation.

## Configuration

Use the `AddGZipContributor` extension method on a container configuration, after every other contributor of the container:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    options.Containers.Configure<AttachmentContainer>(container =>
    {
        container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 20 * 1024 * 1024);
        container.AddGZipContributor(c => c.CompressionLevel = CompressionLevel.Fastest);
    });
});
````

`CompressionLevel` (`System.IO.Compression.CompressionLevel`, default `Optimal`) is the only option. Changing it does not affect reading the BLOBs that already exist.

> Add GZip **last**. Once the content is compressed, it is no longer an image and no longer something the MIME type detector can read, so every contributor that inspects the content must run before it. See [The Recommended Order](index.md#the-recommended-order).

## How It Works

While **saving**, the content is compressed into memory, and the compressed stream, with a known length, replaces the content stream. The bytes read from the input are counted and capped at the container's `MaxSizeInBytes` or, without a [size limit](max-size.md), at 100 MB: the save fails with `ContentTooLarge` as soon as the input passes the cap.

While **reading**, the stored stream is wrapped in a decompressing stream. The stream `GetAsync` returns is **read-only and not seekable**, and its `Length` is not available; read it to the end instead of relying on its length.

## Changing the Configuration of a Container

GZip **changes the stored format**. A BLOB can only be read with the same transforming contributors, in the same order, it was saved with, so adding GZip to, or removing it from, a container that already has BLOBs makes the existing BLOBs fail to read.

To change it on a container with existing BLOBs:

1. Read every BLOB with the **old** configuration and export the plain content.
2. Change the configuration.
3. Write the content back.

Re-saving the BLOBs in place under the old configuration does not change their stored form. The compression level is the exception: it only affects newly saved BLOBs, so it can be changed at any time.

## See Also

* [Stored Format](index.md#stored-format)
* [BLOB Content Pipeline](https://abp.io/docs/latest/framework/infrastructure/blob-storing/pipeline)
