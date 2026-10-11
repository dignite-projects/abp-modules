# Dignite Abp Blob Storing

> Part of [**dignite-projects/abp-modules**](https://github.com/dignite-projects/abp-modules) — see
> the [repository README](../README.md) for the other modules, and
> [CONTRIBUTING.md](../CONTRIBUTING.md) for the build, versioning, and release process shared across
> them. Formerly developed at `dignite-projects/abp-file-storing` and published as
> `Dignite.Abp.FileStoring` until `10.0.0-rc.27`; the packages were renamed to follow ABP's naming
> (see [`docs/pipeline-contributors-decision.md`](docs/pipeline-contributors-decision.md)).

Everyday `IBlobPipelineContributor` implementations for
[ABP BlobStoring](https://abp.io/docs/latest/framework/infrastructure/blob-storing). ABP 10.7 added the BLOB
content pipeline — per-container contributors that transform or validate the content inside
`IBlobContainer.SaveAsync` and `GetAsync` — but ships the mechanism without any contributors. This module ships the
ones nearly every application ends up writing: a size cap, a content-type allow-list based on the real bytes, GZip,
and image resize and compress.

There is no upload entry point, return object, blob naming or metadata here. You keep calling `IBlobContainer`; the
contributors run inside it. Whatever your application records about a file — its name, MIME type, hash, owner — and
who may read it stay in your application.

## Packages

| Package | Contains | Depends on |
|---|---|---|
| `Dignite.Abp.BlobStoring.Pipeline` | `MaxSize`, `AllowedContentTypes` and `GZip` contributors; `IMimeTypeDetector`, the content-based MIME detector they share; `BlobStreamBuffering`, capped buffering for contributors | `Volo.Abp.BlobStoring`, FileSignatures (MIT) |
| `Dignite.Abp.BlobStoring.Imaging` | `ImageResize` and `ImageCompress` contributors; the image decode guard | `Dignite.Abp.BlobStoring.Pipeline`, `Volo.Abp.Imaging.Abstractions` only |

The Imaging package references no image library. You choose one of ABP's imaging providers
(`Volo.Abp.Imaging.SkiaSharp`, `Volo.Abp.Imaging.ImageSharp` or `Volo.Abp.Imaging.MagickNet`) and add it to your
application yourself.

## Installation

```bash
dotnet add package Dignite.Abp.BlobStoring.Pipeline
dotnet add package Dignite.Abp.BlobStoring.Imaging      # only for the image contributors
dotnet add package Volo.Abp.Imaging.SkiaSharp           # one imaging provider, with the Imaging package
```

Depend on the modules, and on the provider's module next to `DigniteAbpBlobStoringImagingModule`:

```csharp
[DependsOn(
    typeof(DigniteAbpBlobStoringPipelineModule),
    typeof(DigniteAbpBlobStoringImagingModule),
    typeof(AbpImagingSkiaSharpModule))]   // or AbpImagingImageSharpModule / AbpImagingMagickNetModule
public class MyAppModule : AbpModule
{
}
```

Notes on choosing the provider:

- Without a provider, `ImageResize` and `ImageCompress` find nothing that can handle an image and store it unchanged.
- ABP's SkiaSharp package brings the Windows and macOS native libraries only; on Linux also reference
  `SkiaSharp.NativeAssets.Linux`.
- ABP's ImageSharp provider currently resolves SixLabors.ImageSharp 3.1.11, which carries High-severity advisories.
  The fixed releases (3.2.0 and later, and 4.x) need a Six Labors license key at build time, which is why the
  package does not pick the provider for you.

## Configuration

Contributors are configured per container. They run in the order they are added when saving, and in reverse when
reading. ABP's built-in encryption, when enabled on the container, always runs after the contributors on save.

```csharp
Configure<AbpBlobStoringOptions>(options =>
{
    // Documents: capped, restricted to a few real types, stored GZip-compressed.
    options.Containers.Configure<AttachmentsContainer>(container =>
    {
        container.UseFileSystem(fs => fs.BasePath = "/var/app/blobs/attachments");

        container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 20 * 1024 * 1024);
        container.AddAllowedContentTypesContributor(c =>
            c.AllowedContentTypes = ["application/pdf", "text/plain", "text/csv", "application/json"]);
        container.AddGZipContributor(c => c.CompressionLevel = CompressionLevel.Fastest);
    });

    // Pictures: capped, images only, bounded before decoding, shrunk and re-compressed.
    options.Containers.Configure<PicturesContainer>(container =>
    {
        container.UseFileSystem(fs => fs.BasePath = "/var/app/blobs/pictures");

        container.AddMaxSizeContributor(c => c.MaxSizeInBytes = 12 * 1024 * 1024);
        container.AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["image/*"]);
        container.ConfigureImageDecodeGuard(g =>        // defaults: 8192 x 8192, 50,000,000 pixels, 10 s
        {
            g.MaxSourceWidth = 8000;
            g.MaxSourceHeight = 8000;
            g.MaxSourcePixels = 30_000_000;
            g.DecodeTimeout = TimeSpan.FromSeconds(5);
        });
        container.AddImageResizeContributor(c =>
        {
            c.MaxWidth = 2048;
            c.MaxHeight = 2048;
            c.MinWidth = 64;
            c.MinHeight = 64;
        });
        container.AddImageCompressContributor();
    });
});
```

Each `Add…Contributor` applies your configuration, validates it and appends the contributor once; calling it again
only updates the configuration. An invalid setup throws `AbpException` there (and again on the first save, should the
configuration be changed afterwards).

## Contributors

### MaxSize

`AddMaxSizeContributor(c => c.MaxSizeInBytes = …)` rejects content larger than the limit with
`Dignite.Abp.BlobStoring:ContentTooLarge`.

- **On save:** the limit binds while the content is read. A seekable stream whose remaining length already exceeds the
  limit is rejected without reading; any other stream is copied chunk by chunk into memory and rejected as soon as the
  copy passes the limit. Nothing beyond the limit is buffered and nothing is saved when it throws. The copy then replaces
  the stream, so everything after it works on a seekable stream with a known length — which is also what storage
  providers that need the object size before uploading want. Add it first.
- **On get:** does nothing.
- **Configuration:** `MaxSizeInBytes` (`long`), no default; must be greater than 0 and at most `Array.MaxLength`
  (about 2 GB), because the content is held in memory.
- **Changes the stored format:** no; it can be added to or removed from a container that already has blobs.

It is the only contributor that limits the size of a *seekable* stream. Still enforce a request-body limit at the HTTP
layer, so an oversized upload is cut off before it reaches your code.

### AllowedContentTypes

`AddAllowedContentTypesContributor(c => c.AllowedContentTypes = […])` rejects content whose detected type is not on the
list with `Dignite.Abp.BlobStoring:ContentTypeNotAllowed`. The type is detected from the bytes by
[`IMimeTypeDetector`](#imimetypedetector), never taken from the uploader; the extension of the blob name is
reconciled with it, and content that contradicts the extension fails with `Dignite.Abp.BlobStoring:ContentTypeMismatch`.

- **On save:** a stream that is seekable and at its start is probed in place and rewound. Any other stream is first
  copied into memory, capped at the container's `MaxSizeInBytes` or, without one, at
  `BlobStoringPipelineConsts.DefaultMaxBufferedBytes` (100 MB; a static property, so it can be changed once at startup),
  and fails with `ContentTooLarge` beyond it; the copy replaces the stream.
- **On get:** does nothing.
- **Configuration:**
  - `AllowedContentTypes` (`string[]`), required: exact types (`application/pdf`) or a wildcard over a top-level type
    (`image/*`), compared case-insensitively. Each entry must be `type/subtype` or `type/*`.
  - `AllowUnidentified` (`bool`, default `false`): whether content the detector cannot identify
    (`application/octet-stream`) is accepted. Listing `application/octet-stream` itself has the same effect;
    `application/*` does not.
- **Changes the stored format:** no.

`image/*` admits every image type, `image/svg+xml` among them. SVG is markup, not a raster image: list the exact raster
types (`image/png`, `image/jpeg`, …) if a container must not take it.

### GZip

`AddGZipContributor(c => c.CompressionLevel = …)` stores the content GZip-compressed and decompresses it when it is read.

- **On save:** the content is compressed eagerly into memory, leaving the received stream open, and the compressed
  stream (with a known length) replaces it. The compressed result is held in memory without a limit of its own: put
  `MaxSize` in front of it on every container that takes uploads.
- **On get:** the stored stream is wrapped in a decompressing `GZipStream` that disposes it. The stream `GetAsync`
  returns is read-only and not seekable.
- **Configuration:** `CompressionLevel` (`System.IO.Compression.CompressionLevel`, default `Optimal`). Changing it does
  not affect reading existing blobs.
- **Changes the stored format:** yes — see [Stored format](#stored-format).

### ImageResize

`AddImageResizeContributor(c => …)` (package `Dignite.Abp.BlobStoring.Imaging`) shrinks images that exceed a maximum
size, using ABP's `IImageResizer`, and rejects images smaller than a minimum with
`Dignite.Abp.BlobStoring.Imaging:ImageTooSmall`.

- **On save:** the content is made seekable (capped like `AllowedContentTypes`), its type is detected, and anything that
  is not `image/*` passes through untouched — a container may hold mixed content, so add
  `AddAllowedContentTypesContributor` to forbid anything but images. For an image, the [decode guard](#decode-guard)
  checks the dimensions read from the header before anything decodes it. The image is resized only when it exceeds a
  constrained dimension, so an image that already fits is stored byte for byte and never upscaled. An image the
  provider does not support (SkiaSharp resizes JPEG, PNG and WebP only) is stored unchanged. If the header gives no
  dimensions, the resizer is called regardless and what happens to an image that already fits is up to the provider.
- **On get:** does nothing.
- **Configuration:**
  - `MaxWidth`, `MaxHeight` (`int`, default 0 = unconstrained): at least one must be greater than 0.
  - `MinWidth`, `MinHeight` (`int`, default 0 = no minimum): a smaller source image is rejected. Enforced only when the
    header gives the dimensions.
  - `Mode` (`Volo.Abp.Imaging.ImageResizeMode`, default `Max`: fit inside the box, keeping the aspect ratio).
  - None may be negative.
- **Changes the stored format:** no. The result is an ordinary image, nothing is undone on read, and blobs saved before
  the contributor was added keep reading as they were stored.

### ImageCompress

`AddImageCompressContributor()` re-encodes images with ABP's `IImageCompressor`, keeping their format and dimensions.

- **On save:** the same flow as `ImageResize` (seekable copy, type detection, non-images pass through, decode guard).
  The compressed image replaces the content only when the provider reports success. ABP's compressors report
  "canceled" when the re-encoded image is not smaller, and the original is kept then; a provider that does not support
  the format (SkiaSharp and ImageSharp compress JPEG, PNG and WebP) leaves the image as it is.
- **On get:** does nothing.
- **Configuration:** none of its own. The encoder settings belong to the provider (for example
  `SkiaSharpCompressOptions.Quality`); the limits are the decode guard's.
- **Changes the stored format:** no.

### Decode guard

`ConfigureImageDecodeGuard(g => …)` sets the limits both image contributors apply before an image reaches the imaging
provider, which decodes it into memory. It does not add a contributor, and it is optional: the defaults apply when it is
never called.

| Property | Default | Meaning |
|---|---|---|
| `MaxSourceWidth` | 8192 | Widest source image, in pixels |
| `MaxSourceHeight` | 8192 | Tallest source image, in pixels |
| `MaxSourcePixels` | 50,000,000 | Width × height; admits a 24 MP camera original. A decoded image takes about four bytes per pixel, so this is roughly 200 MB per decode in the worst case — lower it for containers that take uploads from many users at once |
| `MaxDecompressionRatio` | 100 | Most pixels a source image may declare per stored byte; rejects a small file that declares a huge canvas |
| `DecodeTimeout` | 10 seconds | How long the provider may take to process one image |

Every value must be greater than 0, and the timeout at most `int.MaxValue` milliseconds. An image over a size limit fails
with `Dignite.Abp.BlobStoring.Imaging:ImageTooLarge`.

The dimensions are read from the image header, without decoding, for PNG, JPEG, GIF, BMP, WebP and TIFF. For any other
format, or a header the reader does not understand, the dimension, pixel and ratio checks are skipped; the decode timeout
and the container's buffering cap still apply.

Processing that exceeds `DecodeTimeout` fails with `ImageDecodeTimeout`; a provider that throws on the content (a corrupt
image) fails with `ImageProcessingFailed`. See [Known limitations](#known-limitations) for how far the timeout reaches.

## IMimeTypeDetector

`IMimeTypeDetector.DetectAsync(Stream stream, string? fileName = null, CancellationToken = default)` decides the MIME
type of content from its bytes. The contributors use it, and an application can call it too (see
[What the caller still does](#what-the-caller-still-does)). The stream must be seekable; it is read from position 0 and
its position is restored to 0.

**The engine** is [FileSignatures](https://github.com/neilharvey/FileSignatures) (MIT) behind
`IFileFormatInspector`, which also looks inside archives, so an Office Open XML or OpenDocument file is told apart from
a plain ZIP. Signatures short enough to occur in text (`BM`, `MZ`, `ID3`, …) must be confirmed by the rest of the
header. A header probe covers formats FileSignatures does not know (WAV, AVIF and HEIC, other ISO media brands, MP3 frames
without an ID3 tag, generic compound files, empty ZIPs). Only the first 1,445 bytes are sampled, plus the archive
structure for ZIP-based formats; a ZIP whose `[Content_Types].xml` entry is larger than 1 MB is not opened and is
classified as a plain ZIP.

**The policy** on top of it:

- **Content first.** Content with a known signature decides the type.
- **The extension is reconciled by kind.** If `fileName` has an extension of a different kind than the content (an
  executable named `.png`, a ZIP named `.pdf`, a PNG named `.txt`), the content is rejected with
  `Dignite.Abp.BlobStoring:ContentTypeMismatch`. Within one kind the content wins (a PNG named `.jpg` is `image/png`),
  except for generic containers, where the extension says which format the container holds (a ZIP named `.epub` is
  `application/epub+zip`).
- **`RequiresSignature`.** Each known extension is marked as either always carrying a signature (`.png`, `.pdf`, `.docx`,
  `.zip`, `.exe`, …) or not (`.txt`, `.svg`, `.mp4`, `.mp3`, `.tar`, …). Content without a recognised signature that is
  named with the first kind is rejected as a mismatch; with the second kind it takes the extension's type.
- **Text sniff.** Content without a signature is tested for text (no WHATWG "binary data byte"; UTF-8 validity is not
  required, so legacy encodings still count). HTML, SVG and XML are recognised from their markup (the root element, after
  skipping a byte order mark, comments, processing instructions and a doctype); other text takes the extension's text
  type or `text/plain`. All text types are one kind, so HTML named `.txt` is `text/html`, while text named `.png` is a
  mismatch.
- **Unknown is `application/octet-stream`.** That includes binary content with an unknown or missing extension. An empty
  stream takes its extension's type, unless that format requires a signature.

Only the *extension* of `fileName` matters, so name your blobs with their real extension if you want the contributors to
reconcile it, or call the detector yourself with the original file name.

**Replacing it.** The detector is transient and every member is `virtual` or `protected virtual`
(`DetectFromSignature`, `DetectFromContent`, `DetectTextFormat`, `GetMimeTypeFromExtension`, `GetKind`,
`IsGenericContainer`, …). Subclass it and register the subclass with `[Dependency(ReplaceServices = true)]`, or register
your own `IFileFormatInspector` to teach the default new formats.

**What it does not do.** Magic bytes are necessary, not sufficient:

- A *polyglot* — a file that is a valid image and also valid script, HTML or archive — passes a signature check. The
  detector tells you what the file claims to be at its start, not that nothing else is hidden in it.
- An allowed type can still be hostile (a PDF with embedded script, an Office file with macros). Allow only the types a
  container needs; the detector recognises macro-enabled Office types, so leaving them off the list rejects them.
- Serve user content with `X-Content-Type-Options: nosniff` and, for anything that is not meant to render inline,
  `Content-Disposition: attachment`, ideally from a separate origin.
- The image contributors help for images: when the provider actually produces a new image (a resize that resized, a
  compression that came out smaller), what is stored is a re-encode of the pixels, which is what the OWASP File Upload
  guidance calls image rewriting. An image that already fits, or that does not compress, is stored as uploaded.

## What the caller still does

This module does not wrap `IBlobContainer`. The application names the blob, records what it needs about the file, and
cleans up when its own write fails. Detecting the type and hashing are the caller's if its metadata row wants them:

```csharp
public async Task<Attachment> UploadAsync(Guid ticketId, string fileName, Stream upload, CancellationToken cancellationToken)
{
    // One capped, seekable copy: detecting and hashing both read it. Use the container's MaxSize here.
    await using var content = await BlobStreamBuffering.CopyToBufferAsync(upload, MaxUploadBytes, cancellationToken);

    var mimeType = await _mimeTypeDetector.DetectAsync(content, fileName, cancellationToken);   // may throw ContentTypeMismatch
    var hash = Convert.ToHexString(await SHA256.HashDataAsync(content, cancellationToken));
    content.Position = 0;

    var blobName = GuidGenerator.Create().ToString("N");                                         // you name the blob
    await _container.SaveAsync(blobName, content, overrideExisting: false, cancellationToken);   // the contributors run here

    try
    {
        return await _attachments.InsertAsync(
            new Attachment(GuidGenerator.Create(), ticketId, blobName, fileName, mimeType, content.Length, hash),
            autoSave: true,
            cancellationToken: cancellationToken);
    }
    catch
    {
        await _container.DeleteAsync(blobName, CancellationToken.None);   // the request may be cancelled; the cleanup must not be
        throw;
    }
}
```

- `overrideExisting: false` makes a name collision a `BlobAlreadyExistsException` instead of a silent overwrite. Never
  "compensate" that by deleting: the blob belongs to someone else.
- The type, hash and size above describe the *upload*. If a resize, a compression or GZip changes the stored bytes,
  they no longer describe what is stored. A row that needs the stored size or hash reads the blob back with `GetAsync`
  (image contributors store ordinary images; GZip is undone on read).
- Store the blob first, write the row second, delete the blob if the row fails. Do not reach for an outbox to make an
  in-request file write atomic.
- Dedup by content, who may read or delete a blob, and tenant scoping beyond what BlobStoring itself does are all
  application concerns.

## Ordering

Add contributors in this order:

1. **Validation** — `MaxSize`, then `AllowedContentTypes`. Reject early and cheaply, before any transform.
   `MaxSize` goes first because it caps what everything after it buffers and hands them a seekable stream with a known
   length, so nothing downstream copies the content again.
2. **Image contributors** — `ImageResize`, then `ImageCompress` (so the resized image is the one compressed), after the
   validation that proves the content is an image of an allowed type.
3. **GZip** — last. Once the content is compressed it is no longer an image and no longer something a detector can
   read, so anything that has to inspect the content comes before it.

On get the contributors run in reverse; only GZip does anything then.

## Stored format

`MaxSize`, `AllowedContentTypes`, `ImageResize` and `ImageCompress` do not change what is stored beyond what they did to
that one upload: you can add them to, or remove them from, a container that already has blobs, and existing blobs read as
before. The image contributors are one-way transforms, which is why adding them later breaks nothing.

**GZip is part of the stored format.** A blob can only be read with the same transforming contributors, in the same order,
that saved it. Adding GZip to (or removing it from) a container that already has blobs makes the existing blobs fail to
read. To change it, read every blob with the old configuration and write it back with the new one; the compression level
is the exception, since it only affects newly saved blobs.

## Known limitations

- **The decode timeout is cooperative.** `DecodeTimeout` cancels the token it passes to the imaging provider, so it only
  interrupts a provider that observes that token while decoding. ImageSharp does; SkiaSharp and Magick.NET decode
  synchronously. For those, the header-based dimension guard and the byte cap are the protections that apply.
- **Memory.** The size cap, type check and image contributors hold the upload in memory (that is what makes it
  re-readable), so a limit above about 2 GB is not supported, and concurrent large uploads multiply the cost.
- **Detection is signature-based.** See [What it does not do](#imimetypedetector).

## Roadmap

Candidates for a second batch:

- `Watermark`
- `SvgSanitize`
- `ImageFormatConvert`
- ClamAV scan (as its own package, so the core stays free of a scanner dependency)
- `ImageMetadataStrip` — held back because ABP's imaging abstraction has no such operation and the providers differ
  (ImageSharp and Magick.NET keep EXIF data when they re-encode; SkiaSharp drops it), so it would have to be
  provider-specific

Not planned:

- Thumbnails, deduplication and hash sidecars — a contributor is a one-to-one stream transform and cannot produce a second
  blob
- PDF and Office document disarming
- Audit events

## Error codes

All are `BusinessException` codes, localized in English, Japanese, Simplified Chinese and Traditional Chinese. The two
packages use two code namespaces because ABP maps one code namespace to one localization resource.

| Code | Thrown by | Data |
|---|---|---|
| `Dignite.Abp.BlobStoring:ContentTooLarge` | `MaxSize`; the capped copy of `AllowedContentTypes` and the image contributors | `MaxSizeInBytes` |
| `Dignite.Abp.BlobStoring:ContentTypeMismatch` | `IMimeTypeDetector` (so also `AllowedContentTypes` and the image contributors) | `Extension` |
| `Dignite.Abp.BlobStoring:ContentTypeNotAllowed` | `AllowedContentTypes` | `ContentType` |
| `Dignite.Abp.BlobStoring.Imaging:ImageTooLarge` | the decode guard (both image contributors) | `Width`, `Height`, `MaxWidth`, `MaxHeight`, `MaxPixels`, `MaxDecompressionRatio` |
| `Dignite.Abp.BlobStoring.Imaging:ImageTooSmall` | `ImageResize` | `Width`, `Height`, `MinWidth`, `MinHeight` |
| `Dignite.Abp.BlobStoring.Imaging:ImageDecodeTimeout` | both image contributors | `DecodeTimeoutSeconds` |
| `Dignite.Abp.BlobStoring.Imaging:ImageProcessingFailed` | both image contributors, when the provider throws on the content or ends a resize canceled | — |

An invalid contributor configuration is an `AbpException`, not a business exception: it is a bug in the application, not
a bad upload.
