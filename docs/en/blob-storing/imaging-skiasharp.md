```json
//[doc-seo]
{
    "Description": "Store JPEGs and PNGs saved to an ABP BLOB container as WebP, and turn photos upright by their EXIF orientation before ABP's SkiaSharp resizer shrinks them."
}
```

# SkiaSharp Image Contributors

The `Dignite.Abp.BlobStoring.Imaging.SkiaSharp` package adds what ABP's provider-agnostic [image manipulation](https://abp.io/docs/latest/framework/infrastructure/image-manipulation) abstractions cannot express, on ABP's SkiaSharp provider:

* **JPEG to WebP**: A JPEG saved to a container is stored as a lossy WebP, turned upright by its EXIF orientation and fitted into the container's resize box in the same decode.
* **PNG to WebP**: A PNG is stored as a lossless WebP, transparency and all, fitted into the box the same way - unless it is small, has 16 bits per channel or is animated.
* **Upright resizing**: ABP's SkiaSharp resizer is replaced by one that turns a photo upright before it shrinks it, so the [image resize](imaging.md#image-resize) contributor no longer stores a portrait photo on its side, and that keeps an animated picture as it is.

Unlike the [Imaging package](imaging.md), which references no image library, this package is tied to SkiaSharp: both features decode the picture themselves.

## Installation

Install the [Dignite.Abp.BlobStoring.Imaging.SkiaSharp](https://www.nuget.org/packages/Dignite.Abp.BlobStoring.Imaging.SkiaSharp) package and depend on its module:

````bash
dotnet add package Dignite.Abp.BlobStoring.Imaging.SkiaSharp
````

````csharp
[DependsOn(typeof(AbpBlobStoringImagingSkiaSharpModule))]
public class MyProjectModule : AbpModule
{
}
````

`AbpBlobStoringImagingSkiaSharpModule` is in the `Dignite.Abp.BlobStoring.Imaging` namespace. It depends on `AbpBlobStoringImagingModule` and on ABP's `AbpImagingSkiaSharpModule`, so the Imaging and Pipeline packages and the SkiaSharp provider come with it.

> SkiaSharp brings its native libraries for Windows and macOS only. **On Linux**, also reference the `SkiaSharp.NativeAssets.Linux` package in the host (the same version as the `SkiaSharp` package your build resolves), or every picture fails with `Dignite.Abp.BlobStoring.Imaging:ImageProcessingFailed`.

## Storing JPEGs as WebP

Use the `StoreJpegAsWebp` extension method after the [allowed content types](allowed-content-types.md) contributor and **before** the [image resize](imaging.md#image-resize) contributor - and, with `StorePngAsWebp`, before the [metadata](imaging.md#image-metadata) contributor:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    options.Containers.Configure<PictureContainer>(container =>
    {
        container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 25 * 1024 * 1024);
        container.AddAllowedContentTypesContributor(c =>
            c.AllowedContentTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"]);
        container.StoreJpegAsWebp(quality: 85);
        container.StorePngAsWebp();
        container.AddImageMetadataStripContributor();
        container.AddImageResizeContributor(c =>
        {
            c.MaxWidth = 2560;
            c.MaxHeight = 4096;
        });
    });
});
````

* `quality` (`int`, 1-100, default `85`): The quality of the lossy WebP. Calling `StoreJpegAsWebp` again only changes it.

What it does with a JPEG:

1. Decodes it once, after the [image decode guard](imaging.md#image-decode-guard) has checked its dimensions.
2. Turns it upright by its EXIF orientation. The WebP keeps no EXIF, so the pixels themselves have to be upright, or a browser would show a phone's portrait photo on its side.
3. When the container has an image resize contributor in its default `Max` mode, shrinks the upright picture to fit inside that box, keeping its aspect ratio, sampling with `SkiaSharpResizerOptions.SKSamplingOptions`. The resize contributor then finds a WebP that already fits and leaves it alone, so the photo is encoded once. With another resize mode, the WebP is stored at its upright size and the resize contributor applies its mode to it.
4. Encodes it as lossy WebP at the configured quality.

Every JPEG is converted, whatever the size of the result, except one that would still be wider or taller than WebP's limit of 16383 pixels once fitted into the box - possible only when the container has no box, leaves a side unconstrained, or has a [decode guard](imaging.md#image-decode-guard) raised above it - or whose header gives no size: that one is stored as a JPEG. Any other content passes through to the contributors after it. A JPEG that SkiaSharp cannot decode fails with `Dignite.Abp.BlobStoring.Imaging:ImageProcessingFailed`.

Like the other image contributors, it is a one-way transformation: the stored WebP is read as it is. It can be added to a container that already has BLOBs; the JPEGs saved before stay JPEGs.

## Storing PNGs as WebP

Use the `StorePngAsWebp` extension method after the allowed content types contributor and **before any contributor that changes the bytes** - the metadata and image resize contributors - as in the example above. Its rule reads the length of the upload, which has to be the length your application saves.

* `minSizeInBytes` (`long`, default `16384`): The size from which a PNG is stored as WebP; a smaller one is stored as it is. `0` converts every PNG the other rules allow. Calling `StorePngAsWebp` again only changes it.

A PNG is decoded, turned and fitted into the box as a JPEG is, and encoded as **lossless** WebP: a PNG that already fits keeps every visible pixel, its transparency included - WebP has an alpha channel in both its modes. A PNG is stored as it is when:

* It is smaller than `minSizeInBytes`. Lossless WebP is about [26% smaller than PNG](https://developers.google.com/speed/webp), so from 16 KB a conversion saves about 4 KB, the least saving that Chrome DevTools' image delivery insight reports as worth a modern format.
* It has 16 bits per channel. WebP has 8.
* It is animated: an APNG would keep one frame.
* It would still exceed WebP's 16383 pixels once fitted into the box, or its header gives no size, as for a JPEG.

A PNG that has to stay a PNG wherever it is used - a favicon, a picture sent by email - belongs in a container without `StorePngAsWebp`.

### The Stored Content Type

The container knows **before the save** what it will store: `StoreJpegAsWebp` and `StorePngAsWebp` declare it on the container's configuration, each with its rule, and the contributors convert by that declaration. An application that names its BLOBs or records their type reads it first, from the content it is about to save:

````csharp
// IBlobContainerConfigurationProvider; "image/webp" for a JPEG, or a PNG the rules convert
var storedType = _blobContainerConfigurationProvider
    .Get(BlobContainerNameAttribute.GetContainerName<PictureContainer>())
    .GetStoredContentType(mimeType, content);
````

`content` is the seekable stream you are about to save; the rules read its header and its length, and its position is restored. `GetStoredContentType` returns the type itself when nothing declared a conversion for it. It is a declaration, not a result of the save: an application that has to know what was actually stored detects it from the stored bytes afterwards. See [Saving Uploaded Files](saving-uploaded-files.md#when-the-container-converts-the-content).

## Upright Resizing

A camera stores a portrait photo sideways and records the turn in its EXIF orientation. ABP 10.7.0's SkiaSharp resizer ignores the orientation, and the picture it encodes has no EXIF, so a photo the [image resize](imaging.md#image-resize) contributor shrinks is stored on its side.

`AbpBlobStoringImagingSkiaSharpModule` removes ABP's `SkiaSharpImageResizerContributor` and registers `UprightSkiaSharpImageResizerContributor` in its place. It decodes, resizes and encodes as ABP's does, with ABP's own resize code for every `ImageResizeMode`, but turns the picture upright first. **This applies to every caller of `IImageResizer` in the application**, not only to BLOB containers. The image resize contributor measures a picture as displayed too, so a photo taller than the box once upright is shrunk, and one that fits once upright is stored as uploaded.

An animated picture - an APNG or an animated WebP - is left alone: decoded into a still bitmap, it would keep one frame. The resize contributor stores it as uploaded.

> ABP fixed the orientation defect on its `rel-10.7` branch after 10.7.1. Once this package is built on an ABP release that has the fix, the orientation part of the replacement is no longer needed and will be removed.

The module also changes the defaults of `SkiaSharpResizerOptions`, for every caller as well:

| Option | ABP's default | This module's default |
|---|---|---|
| `SKSamplingOptions` | `SKSamplingOptions.Default` (nearest neighbour) | Linear filtering between linear-blended mipmap levels |
| `Quality` | `75` | `85` |

Nearest-neighbour sampling keeps one source pixel per target pixel and drops the rest, so a shrunk photo turns fine detail into jagged noise and moiré; linear sampling between mipmap levels averages instead. To keep ABP's values, configure them in your own module, which runs after this one:

````csharp
Configure<SkiaSharpResizerOptions>(options =>
{
    options.SKSamplingOptions = SKSamplingOptions.Default;
    options.Quality = 75;
});
````

## Turning Pictures Upright Yourself

Code that decodes a stored picture to render it, such as an endpoint that resizes on request, has the same problem with a JPEG or PNG stored as uploaded. `ImageOrientation.Orient(bitmap, codec.EncodedOrigin)` returns the bitmap turned upright (the same bitmap when it is upright already, otherwise a new one you dispose); the bitmap has to have 32-bit pixels.

## See Also

* [Image Contributors](imaging.md)
* [Image Manipulation](https://abp.io/docs/latest/framework/infrastructure/image-manipulation)
* [Saving Uploaded Files](saving-uploaded-files.md)
