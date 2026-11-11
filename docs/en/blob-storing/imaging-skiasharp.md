```json
//[doc-seo]
{
    "Description": "Store JPEGs saved to an ABP BLOB container as WebP, and turn photos upright by their EXIF orientation before ABP's SkiaSharp resizer shrinks them."
}
```

# SkiaSharp Image Contributors

The `Dignite.Abp.BlobStoring.Imaging.SkiaSharp` package adds what ABP's provider-agnostic [image manipulation](https://abp.io/docs/latest/framework/infrastructure/image-manipulation) abstractions cannot express, on ABP's SkiaSharp provider:

* **JPEG to WebP**: Every JPEG saved to a container is stored as a lossy WebP, turned upright by its EXIF orientation and fitted into the container's resize box in the same decode.
* **Upright resizing**: ABP's SkiaSharp resizer is replaced by one that turns a photo upright before it shrinks it, so the [image resize](imaging.md#image-resize) contributor no longer stores a portrait photo on its side.

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

Use the `StoreJpegAsWebp` extension method, after the [metadata](imaging.md#image-metadata) contributor and **before** the [image resize](imaging.md#image-resize) contributor:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    options.Containers.Configure<PictureContainer>(container =>
    {
        container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 25 * 1024 * 1024);
        container.AddAllowedContentTypesContributor(c =>
            c.AllowedContentTypes = ["image/jpeg", "image/png", "image/gif", "image/webp"]);
        container.AddImageMetadataStripContributor();
        container.StoreJpegAsWebp(quality: 85);
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
2. Turns it upright by its EXIF orientation. The WebP keeps no EXIF, so the pixels themselves have to be upright, or a browser would show a phone's portrait photo on its side. The metadata contributor keeps EXIF, so it can come first.
3. When the container has an image resize contributor in its default `Max` mode, shrinks the upright picture to fit inside that box, keeping its aspect ratio, sampling with `SkiaSharpResizerOptions.SKSamplingOptions`. The resize contributor then finds a WebP that already fits and leaves it alone, so the photo is encoded once. With another resize mode, the WebP is stored at its upright size and the resize contributor applies its mode to it.
4. Encodes it as lossy WebP at the configured quality.

Every JPEG is converted, whatever the size of the result. A PNG keeps its pixels (a screenshot or a logo, lossless), and a GIF and a WebP are stored as they are; any other content passes through to the contributors after it. A JPEG that SkiaSharp cannot decode fails with `Dignite.Abp.BlobStoring.Imaging:ImageProcessingFailed`.

Like the other image contributors, it is a one-way transformation: the stored WebP is read as it is. It can be added to a container that already has BLOBs; the JPEGs saved before stay JPEGs.

### The Stored Content Type

Because every JPEG is converted, the container knows **before the save** what it will store. `StoreJpegAsWebp` declares it on the container's configuration, and an application that names its BLOBs or records their type reads it first:

````csharp
// IBlobContainerConfigurationProvider; "image/webp" when mimeType is "image/jpeg"
var storedType = _blobContainerConfigurationProvider
    .Get(BlobContainerNameAttribute.GetContainerName<PictureContainer>())
    .GetStoredContentType(mimeType);
````

`GetStoredContentType` returns the type itself when nothing declared a conversion for it. It is a declaration, not a result of the save: an application that has to know what was actually stored detects it from the stored bytes afterwards. See [Saving Uploaded Files](saving-uploaded-files.md#when-the-container-converts-the-content).

## Upright Resizing

A camera stores a portrait photo sideways and records the turn in its EXIF orientation. ABP 10.7.0's SkiaSharp resizer ignores the orientation, and the picture it encodes has no EXIF, so a photo the [image resize](imaging.md#image-resize) contributor shrinks is stored on its side.

`AbpBlobStoringImagingSkiaSharpModule` removes ABP's `SkiaSharpImageResizerContributor` and registers `UprightSkiaSharpImageResizerContributor` in its place. It decodes, resizes and encodes as ABP's does, with ABP's own resize code for every `ImageResizeMode`, but turns the picture upright first. **This applies to every caller of `IImageResizer` in the application**, not only to BLOB containers.

> ABP fixed the same defect on its `rel-10.7` branch after 10.7.1. Once this package is built on an ABP release that has the fix, the replacement is no longer needed and will be removed.

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
