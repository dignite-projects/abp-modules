```json
//[doc-seo]
{
    "Description": "Size limits, content-type allow-lists, GZip, and image resize and compress for ABP BLOB Storing containers, as BLOB pipeline contributors."
}
```

# BLOB Storing Module

ABP 10.7 added the [BLOB content pipeline](https://abp.io/docs/latest/framework/infrastructure/blob-storing/pipeline) to [BLOB Storing](https://abp.io/docs/latest/framework/infrastructure/blob-storing): per-container **contributors** that check or transform the content inside `IBlobContainer.SaveAsync` and `GetAsync`. ABP ships the mechanism, but no contributors. This module provides the ones nearly every application ends up writing:

* [**Size limit**](max-size.md): Rejects content larger than a limit, without buffering more than the limit.
* [**Allowed content types**](allowed-content-types.md): Accepts only the content types you list, detected from the bytes rather than taken from the uploader.
* [**GZip compression**](gzip.md): Stores the content compressed and decompresses it on read.
* [**Image resize and compress**](imaging.md): Shrinks images larger than a maximum size and re-encodes them smaller, with a guard that checks an image's dimensions before anything decodes it.

The contributors share a [MIME type detector](mime-type-detection.md) that your application can call too.

There is **no upload service, return object, BLOB naming or metadata** in this module. You keep using `IBlobContainer`, and the contributors run inside it. Whatever your application records about a file (its name, MIME type, hash, owner) and who may read it stay in your application; [Saving Uploaded Files](saving-uploaded-files.md) shows how the two fit together.

## Packages

| Package | Contents |
|---|---|
| [Dignite.Abp.BlobStoring.Pipeline](https://www.nuget.org/packages/Dignite.Abp.BlobStoring.Pipeline) | The size limit, allowed content types and GZip contributors, `IMimeTypeDetector`, and `BlobStreamBuffering` for your own contributors. Depends on `Volo.Abp.BlobStoring` and [FileSignatures](https://github.com/neilharvey/FileSignatures) (MIT). |
| [Dignite.Abp.BlobStoring.Imaging](https://www.nuget.org/packages/Dignite.Abp.BlobStoring.Imaging) | The image resize and compress contributors and the image decode guard. Depends on `Dignite.Abp.BlobStoring.Pipeline` and `Volo.Abp.Imaging.Abstractions` only. |

> The Imaging package references **no image library**. Your application adds one of ABP's [image manipulation](https://abp.io/docs/latest/framework/infrastructure/image-manipulation) providers itself; see [Choosing an Imaging Provider](imaging.md#choosing-an-imaging-provider).

## Installation

Install the [Dignite.Abp.BlobStoring.Pipeline](https://www.nuget.org/packages/Dignite.Abp.BlobStoring.Pipeline) NuGet package to the project that configures your BLOB containers, and the Imaging package and an imaging provider if you want the image contributors:

````bash
dotnet add package Dignite.Abp.BlobStoring.Pipeline
dotnet add package Dignite.Abp.BlobStoring.Imaging
dotnet add package Volo.Abp.Imaging.SkiaSharp
````

Then add the module dependencies to your [module](https://abp.io/docs/latest/framework/architecture/modularity/basics) class:

````csharp
[DependsOn(
    typeof(AbpBlobStoringPipelineModule),
    typeof(AbpBlobStoringImagingModule),
    typeof(AbpImagingSkiaSharpModule) // or AbpImagingImageSharpModule / AbpImagingMagickNetModule
)]
public class MyProjectModule : AbpModule
{
}
````

`AbpBlobStoringPipelineModule` is in the `Dignite.Abp.BlobStoring.Pipeline` namespace and `AbpBlobStoringImagingModule` in `Dignite.Abp.BlobStoring.Imaging`. A container still needs a [storage provider](https://abp.io/docs/latest/framework/infrastructure/blob-storing#blob-storage-providers); the contributors work on top of any of them.

## Configuring a Container

Contributors are configured **per container**, in the `ConfigureServices` method of your module, with an `Add…Contributor` extension method for each one:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    // Documents: capped, restricted to a few real types, stored compressed
    options.Containers.Configure<AttachmentContainer>(container =>
    {
        container.UseFileSystem(fileSystem =>
        {
            fileSystem.BasePath = "/var/app/blobs/attachments";
        });

        container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 20 * 1024 * 1024);
        container.AddAllowedContentTypesContributor(c =>
            c.AllowedContentTypes = ["application/pdf", "text/plain", "text/csv", "application/json"]);
        container.AddGZipContributor();
    });

    // Pictures: capped, images only, shrunk to 2048 x 2048 and re-compressed
    options.Containers.Configure<PictureContainer>(container =>
    {
        container.UseFileSystem(fileSystem =>
        {
            fileSystem.BasePath = "/var/app/blobs/pictures";
        });

        container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 12 * 1024 * 1024);
        container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["image/*"]);
        container.AddImageResizeContributor(c =>
        {
            c.MaxWidth = 2048;
            c.MaxHeight = 2048;
        });
        container.AddImageCompressContributor();
    });
});
````

* Each `Add…Contributor` method applies your configuration, validates it and appends the contributor to the container's `PipelineContributors` **once**. Calling it again only updates the configuration.
* An invalid configuration throws an `AbpException` right there, and again on the first save if the configuration is changed afterwards. It is a bug in the application, not a bad upload, so it is not a `BusinessException`.
* The contributors run in the order they are added while saving, and in the reverse order while reading. ABP's built-in [BLOB encryption](https://abp.io/docs/latest/framework/infrastructure/blob-storing/encryption), when enabled on the container, always runs after them while saving, so they always see the plain content.
* As with any pipeline contributor, contributors configured on the [default container](https://abp.io/docs/latest/framework/infrastructure/blob-storing/pipeline#configuring-containers) run for every container first.

> The `container.Get…ContributorConfiguration()` extension methods return the current configuration of a contributor, if you need to read it.

## The Recommended Order

Add the contributors of a container in this order:

1. **Validation**: the [size limit](max-size.md) first, then the [allowed content types](allowed-content-types.md). They reject an upload early and cheaply, before any transformation. The size limit goes first because it caps what everything after it buffers, and hands them a seekable stream with a known length, so nothing after it copies the content again.
2. **Images**: [resize, then compress](imaging.md), so the resized image is the one compressed. They come after the validation that proves the content is an image of an allowed type.
3. **GZip**: [last](gzip.md). Once the content is compressed, it is no longer an image and no longer something a detector can read, so anything that inspects the content comes before it.

While reading, the contributors run in reverse, and only GZip does anything then.

## Stored Format

The size limit, allowed content types, image resize and image compress contributors **do not change the stored format**: you can add them to, or remove them from, a container that already has BLOBs, and the existing BLOBs read as before. The image contributors are one-way transformations: what they store is an ordinary image, and nothing is undone on read.

> **GZip is part of the stored format.** A BLOB can only be read with the same transforming contributors, in the same order, it was saved with. Adding GZip to, or removing it from, a container that already has BLOBs makes the existing BLOBs fail to read. See [Changing the Configuration of a Container](gzip.md#changing-the-configuration-of-a-container).

## Exception Handling

The contributors throw [business exceptions](https://abp.io/docs/latest/framework/fundamentals/exception-handling) with the following error codes, localized in English, Japanese, Simplified Chinese and Traditional Chinese. ABP's exception handling returns them to the client as a localized error message, like any other business exception.

| Code | Thrown by | Data |
|---|---|---|
| `Dignite.Abp.BlobStoring:ContentTooLarge` | The size limit, and every other contributor that buffers the content | `MaxSizeInBytes` |
| `Dignite.Abp.BlobStoring:ContentTypeMismatch` | `IMimeTypeDetector`, so also the allowed content types and image contributors | `Extension` |
| `Dignite.Abp.BlobStoring:ContentTypeNotAllowed` | The allowed content types contributor | `ContentType` |
| `Dignite.Abp.BlobStoring.Imaging:ImageTooLarge` | The image decode guard, in both image contributors | `Width`, `Height`, `MaxWidth`, `MaxHeight`, `MaxPixels`, `MaxDecompressionRatio` |
| `Dignite.Abp.BlobStoring.Imaging:ImageTooSmall` | The image resize contributor | `Width`, `Height`, `MinWidth`, `MinHeight` |
| `Dignite.Abp.BlobStoring.Imaging:ImageDecodeTimeout` | Both image contributors | `DecodeTimeoutSeconds` |
| `Dignite.Abp.BlobStoring.Imaging:ImageProcessingFailed` | Both image contributors, when the imaging provider fails on the content | — |

The codes are constants of the `BlobStoringPipelineErrorCodes` and `BlobStoringImagingErrorCodes` classes. The two packages use two code namespaces because ABP maps a code namespace to exactly one localization resource.

## Known Limitations

* **Memory.** The size limit, the allowed content types and the image contributors hold the upload in memory, which is what makes it re-readable. A limit above about 2 GB is not supported, and concurrent large uploads multiply the cost.
* **The decode timeout is cooperative.** It only interrupts an imaging provider that observes the cancellation token while decoding. See [Image Decode Guard](imaging.md#image-decode-guard).
* **Detection is signature-based.** Magic bytes are necessary, not sufficient. See [What the Detector Does Not Do](mime-type-detection.md#what-the-detector-does-not-do).
* **A contributor is a one-to-one transformation.** It cannot produce a second BLOB, so thumbnails, deduplication and hash sidecars are not possible as contributors.

## See Also

* [BLOB Storing](https://abp.io/docs/latest/framework/infrastructure/blob-storing)
* [BLOB Content Pipeline](https://abp.io/docs/latest/framework/infrastructure/blob-storing/pipeline)
* [Saving Uploaded Files](saving-uploaded-files.md)
* [Writing Your Own Contributors](custom-contributors.md)
