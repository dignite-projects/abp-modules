# Dignite Abp Blob Storing

> Part of [**dignite-projects/abp-modules**](https://github.com/dignite-projects/abp-modules) — see
> the [repository README](../README.md) for the other modules, and
> [CONTRIBUTING.md](../CONTRIBUTING.md) for the build, versioning, and release process shared across
> them. Formerly developed at `dignite-projects/abp-file-storing` and published as
> `Dignite.Abp.FileStoring` until `10.0.0-rc.27`; the packages were renamed to follow ABP's naming
> (see [`dev-docs/pipeline-contributors-decision.md`](dev-docs/pipeline-contributors-decision.md)).

Everyday `IBlobPipelineContributor` implementations for
[ABP BlobStoring](https://abp.io/docs/latest/framework/infrastructure/blob-storing). ABP 10.7 added the BLOB
content pipeline — per-container contributors that transform or validate the content inside
`IBlobContainer.SaveAsync` and `GetAsync` — but ships the mechanism without any contributors. This module ships the
ones nearly every application ends up writing: a size cap, a content-type allow-list based on the real bytes, GZip,
and image resize and compress.

There is no upload entry point, return object, blob naming or metadata here. You keep calling `IBlobContainer`; the
contributors run inside it. Whatever your application records about a file — its name, MIME type, hash, owner — and
who may read it stay in your application.

> **.NET 10 · ABP 10.7 · LGPL-3.0-only**

## Documentation

**[Read the documentation](https://github.com/dignite-projects/abp-modules/blob/main/docs/en/blob-storing/index.md)**
for the installation, every contributor and its options, the recommended order, stored formats, MIME type detection,
a complete upload example, writing your own contributors, and the error codes.

## Packages

| Package | Contains |
|---|---|
| `Dignite.Abp.BlobStoring.Pipeline` | `MaxSize`, `AllowedContentTypes` and `GZip` contributors; `IMimeTypeDetector`, the content-based MIME detector they share; `BlobStreamBuffering`, capped buffering for contributors |
| `Dignite.Abp.BlobStoring.Imaging` | `ImageResize` and `ImageCompress` contributors; the image decode guard. References no image library: add one of ABP's imaging providers yourself |

## Quick start

```bash
dotnet add package Dignite.Abp.BlobStoring.Pipeline
dotnet add package Dignite.Abp.BlobStoring.Imaging      # only for the image contributors
dotnet add package Volo.Abp.Imaging.SkiaSharp           # one imaging provider, with the Imaging package
```

```csharp
[DependsOn(
    typeof(AbpBlobStoringPipelineModule),
    typeof(AbpBlobStoringImagingModule),
    typeof(AbpImagingSkiaSharpModule))]   // or AbpImagingImageSharpModule / AbpImagingMagickNetModule
public class MyAppModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure<PicturesContainer>(container =>
            {
                container.UseFileSystem(fs => fs.BasePath = "/var/app/blobs/pictures");

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
    }
}
```

Contributors run in the order they are added when saving, and in reverse when reading. GZip changes the stored
format: do not add it to, or remove it from, a container that already has blobs.

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

## License

[LGPL-3.0-only](https://github.com/dignite-projects/abp-modules/blob/main/LICENSE).
