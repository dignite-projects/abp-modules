```json
//[doc-seo]
{
    "Description": "Remove editing metadata from, resize and compress images saved to an ABP BLOB container, on ABP's image manipulation providers, with a decode guard against oversized images."
}
```

# Image Contributors

The `Dignite.Abp.BlobStoring.Imaging` package provides three contributors that work on images saved to a container:

* The **image metadata** contributor removes the metadata that editing software piles into a JPEG or PNG, without touching the image.
* The **image resize** contributor shrinks images larger than a maximum size, and rejects images smaller than a minimum.
* The **image compress** contributor re-encodes images, keeping their format and dimensions, and stores the result only when it is smaller.

Resize and compress use ABP's [image manipulation](https://abp.io/docs/latest/framework/infrastructure/image-manipulation) abstractions (`IImageResizer` and `IImageCompressor`), and both check an image against the [image decode guard](#image-decode-guard) before anything decodes it. The metadata contributor decodes nothing and needs no provider.

## Installation

Install the [Dignite.Abp.BlobStoring.Imaging](https://www.nuget.org/packages/Dignite.Abp.BlobStoring.Imaging) package together with **one imaging provider**, and depend on both modules:

````bash
dotnet add package Dignite.Abp.BlobStoring.Imaging
dotnet add package Volo.Abp.Imaging.SkiaSharp
````

````csharp
[DependsOn(
    typeof(AbpBlobStoringImagingModule),
    typeof(AbpImagingSkiaSharpModule)
)]
public class MyProjectModule : AbpModule
{
}
````

`AbpBlobStoringImagingModule` depends on `AbpBlobStoringPipelineModule`, so the Pipeline package comes with it.

### Choosing an Imaging Provider

The Imaging package references no image library, so the choice of provider, and its license, stays with your application. ABP has three providers:

* **SkiaSharp** (`Volo.Abp.Imaging.SkiaSharp`, `AbpImagingSkiaSharpModule`): The package brings the native libraries for Windows and macOS only. On Linux, also reference `SkiaSharp.NativeAssets.Linux`. It resizes and compresses JPEG, PNG and WebP.
* **ImageSharp** (`Volo.Abp.Imaging.ImageSharp`, `AbpImagingImageSharpModule`): ABP's provider currently resolves SixLabors.ImageSharp 3.1.11, which has published security advisories. The fixed releases (3.2.0 and later, and 4.x) require a Six Labors license key at build time.
* **Magick.NET** (`Volo.Abp.Imaging.MagickNet`, `AbpImagingMagickNetModule`).

> Without any provider, the resize and compress contributors find nothing that can handle an image, and store every image unchanged.

## Image Metadata

Use the `AddImageMetadataStripContributor` extension method, after the validation contributors and **before** the image resize contributor, so the decode guard and the resize see the image without its metadata:

````csharp
container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 25 * 1024 * 1024);
container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["image/jpeg", "image/png"]);
container.AddImageMetadataStripContributor();
container.AddImageResizeContributor(c =>
{
    c.MaxWidth = 4096;
    c.MaxHeight = 4096;
});
````

Design tools write metadata that no viewer reads, and it can outweigh the image many times. Photoshop records in `photoshop:DocumentAncestors` the ID of every document ever pasted or placed into a file, and the documents those came from; a 1984 x 935 banner exported from it weighed 18 MB, of which the image was 0.9 MB. What the contributor removes:

* From a **JPEG**: Extended XMP (the APP1 segments named `http://ns.adobe.com/xmp/extension/`, where Photoshop puts what does not fit in the main XMP packet), the Photoshop image resources (APP13) and comments (COM).
* From a **PNG**: the text chunks (`tEXt`, `zTXt` and `iTXt`, where Photoshop writes its XMP) and `tIME`.

Everything else is kept **byte for byte**: the image data, the main XMP packet (at most 64 KB, where an HDR gain map and rights statements are declared), EXIF, the ICC profile, the Adobe segment and anything after the image. So the pixels, the colours and the orientation are exactly those saved.

The contributor has no options yet, and it works in managed code without decoding, so the image decode guard does not apply to it. Another format, content whose structure it cannot walk, and an image with nothing to remove are stored unchanged. Like the other image contributors, it does nothing while reading and can be added to a container that already has BLOBs.

> Because EXIF stays, a camera's GPS position stays too. This contributor shrinks files; it is not a privacy filter, and not a sanitizer (see [Image Rewriting](#image-rewriting)).

## Image Resize

Use the `AddImageResizeContributor` extension method on a container configuration:

````csharp
Configure<AbpBlobStoringOptions>(options =>
{
    options.Containers.Configure<PictureContainer>(container =>
    {
        container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 12 * 1024 * 1024);
        container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["image/*"]);
        container.AddImageResizeContributor(c =>
        {
            c.MaxWidth = 2048;
            c.MaxHeight = 2048;
            c.MinWidth = 64;
            c.MinHeight = 64;
        });
    });
});
````

* `MaxWidth`, `MaxHeight` (`int`, default `0`, unconstrained): The largest size an image is stored at. At least one of them must be greater than `0`.
* `MinWidth`, `MinHeight` (`int`, default `0`, no minimum): A smaller image is rejected with the `Dignite.Abp.BlobStoring.Imaging:ImageTooSmall` error code. Enforced only when the image header gives the dimensions.
* `Mode` (`Volo.Abp.Imaging.ImageResizeMode`, default `Max`): How the image is fitted into the maximum size. `Max` fits the image inside the box and keeps its aspect ratio.

None of the values may be negative.

An image is resized **only when it exceeds** a constrained dimension: an image that already fits is stored byte for byte, and an image is never upscaled. An image format the provider does not support is stored unchanged. If the header gives no dimensions, the resizer is called anyway, and what happens to an image that already fits is up to the provider.

## Image Compress

Use the `AddImageCompressContributor` extension method, **after** the image resize contributor, so the resized image is the one compressed:

````csharp
container.AddImageResizeContributor(c =>
{
    c.MaxWidth = 2048;
    c.MaxHeight = 2048;
});
container.AddImageCompressContributor();
````

The image compress contributor has no options of its own. The encoder settings belong to the provider (for example the `SkiaSharpCompressOptions.Quality` of the SkiaSharp provider), and the limits are those of the [image decode guard](#image-decode-guard).

The compressed image replaces the content only when the provider reports success. ABP's compressors report "canceled" when the re-encoded image is **not smaller** than the original, and the original is kept then. A format the provider does not support is stored as it is.

## How They Work

While **saving**, the resize and compress contributors:

1. Make the content seekable, capped at the container's `MaxSizeInBytes` like the [allowed content types](allowed-content-types.md) contributor.
2. Detect its type with the [MIME type detector](mime-type-detection.md). Content that is not `image/*` **passes through untouched**: a container may hold mixed content, so add the allowed content types contributor if a container must accept images only.
3. Check the image against the [image decode guard](#image-decode-guard).
4. Call the provider with a timeout, and use its result.

While **reading**, they do nothing. They do not change the stored format either: what they store is an ordinary image, nothing is undone on read, and the BLOBs saved before a contributor was added keep reading as they were stored.

## Image Decode Guard

An imaging provider decodes an image into memory, about four bytes per pixel, before it can resize or compress it. A small file can declare a huge canvas, so the resize and compress contributors check an image **before** it reaches the provider. The limits are set per container with the `ConfigureImageDecodeGuard` extension method:

````csharp
container.ConfigureImageDecodeGuard(guard =>
{
    guard.MaxSourceWidth = 8000;
    guard.MaxSourceHeight = 8000;
    guard.MaxSourcePixels = 30_000_000;
    guard.DecodeTimeout = TimeSpan.FromSeconds(5);
});
````

`ConfigureImageDecodeGuard` does not add a contributor, and it is optional: the defaults below apply when it is never called.

| Property | Default | Meaning |
|---|---|---|
| `MaxSourceWidth` | `8192` | The widest image accepted, in pixels. |
| `MaxSourceHeight` | `8192` | The tallest image accepted, in pixels. |
| `MaxSourcePixels` | `50,000,000` | Width × height. Admits a 24 MP camera original, which is roughly 200 MB per decode in the worst case. Lower it for containers that take uploads from many users at once. |
| `MaxDecompressionRatio` | `100` | The most pixels an image may declare per stored byte. Rejects a small file that declares a huge canvas. |
| `DecodeTimeout` | 10 seconds | How long the provider may take to process one image. |

Every value must be greater than `0`, and the timeout can be at most `int.MaxValue` milliseconds.

* An image over a size limit is rejected with `Dignite.Abp.BlobStoring.Imaging:ImageTooLarge`.
* Processing that takes longer than `DecodeTimeout` fails with `Dignite.Abp.BlobStoring.Imaging:ImageDecodeTimeout`.
* A provider that fails on the content (a corrupt image) fails with `Dignite.Abp.BlobStoring.Imaging:ImageProcessingFailed`.

The dimensions are read from the image header, without decoding, for PNG, JPEG, GIF, BMP, WebP and TIFF. The reader is public as `ImageHeaderReader.Read(stream)`, for code outside the pipeline that decodes a stored image, such as an endpoint that resizes on request, and should apply the same bound first. For any other format, or a header the reader does not understand, the dimension, pixel and ratio checks are skipped; the decode timeout and the container's buffering cap still apply.

> **The decode timeout is cooperative.** It cancels the token passed to the imaging provider, so it only interrupts a provider that observes the token while decoding. ImageSharp does; SkiaSharp and Magick.NET decode synchronously. With those, the header-based dimension checks and the byte cap are the protections that apply.

## Image Rewriting

When the provider actually produces a new image (a resize that resized, a compression that came out smaller), what is stored is a re-encode of the pixels, which is what the [OWASP File Upload Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html) calls *image rewriting*. An image that already fits, or that does not compress, is stored as uploaded, so do not rely on these contributors to strip content hidden in an image. The metadata contributor does not change that: it removes known metadata and copies everything else, including the bytes after the image.

## See Also

* [Image Manipulation](https://abp.io/docs/latest/framework/infrastructure/image-manipulation)
* [Allowed Content Types](allowed-content-types.md)
* [MIME Type Detection](mime-type-detection.md)
