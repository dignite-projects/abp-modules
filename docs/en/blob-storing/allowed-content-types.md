```json
//[doc-seo]
{
    "Description": "Accept only the content types you list in an ABP BLOB container, detected from the bytes of the content rather than taken from the uploader."
}
```

# Allowed Content Types

The **allowed content types** contributor rejects content whose type is not on a list, with the `Dignite.Abp.BlobStoring:ContentTypeNotAllowed` error code. The type is detected from the **bytes of the content** by the [MIME type detector](mime-type-detection.md), and never taken from what the uploader claims. It is part of the `Dignite.Abp.BlobStoring.Pipeline` package; see the [overview](index.md) for the installation.

## Configuration

Use the `AddAllowedContentTypesContributor` extension method on a container configuration:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    options.Containers.Configure<AttachmentContainer>(container =>
    {
        container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 20 * 1024 * 1024);
        container.AddAllowedContentTypesContributor(c =>
        {
            c.AllowedContentTypes = ["application/pdf", "image/*"];
        });
    });
});
````

* `AllowedContentTypes` (`string[]`): The allowed types, **required**. Each entry is an exact type (`application/pdf`) or a wildcard over a top-level type (`image/*`), compared case-insensitively. Each entry must be in the `type/subtype` or `type/*` form.
* `AllowUnidentified` (`bool`, default `false`): Whether content the detector cannot identify (`application/octet-stream`) is accepted. Listing `application/octet-stream` itself has the same effect; `application/*` does not.

> `image/*` admits **every** image type, `image/svg+xml` among them. SVG is markup that can carry script, not a raster image. List the exact raster types (`image/png`, `image/jpeg`, `image/webp`, ...) if a container must not take SVG.

## The BLOB Name Is Checked Too

The detector also reconciles the content with the **extension of the BLOB name**. Content that contradicts its extension (an executable named `photo.png`, a ZIP file named `report.pdf`) is rejected with the `Dignite.Abp.BlobStoring:ContentTypeMismatch` error code, even when its real type is on the list. See [MIME Type Detection](mime-type-detection.md) for the rules.

Only the extension matters. If you name your BLOBs with a GUID and no extension, this check does not apply; if you want it, name the BLOB with the real extension, or call `IMimeTypeDetector` yourself with the original file name before saving, as [Saving Uploaded Files](saving-uploaded-files.md) shows.

## How It Works

While **saving**:

* A stream that is seekable and at its start is probed in place, and then rewound.
* Any other stream is first copied into memory, and the copy replaces the content stream.

Either way the content is capped at the container's `MaxSizeInBytes` or, without a [size limit](max-size.md), at 100 MB, and content beyond the cap is rejected with `ContentTooLarge`.

While **reading**, it does nothing. It does not change the content, so it can be added to or removed from a container that already has BLOBs.

## See Also

* [MIME Type Detection](mime-type-detection.md)
* [Size Limit](max-size.md)
