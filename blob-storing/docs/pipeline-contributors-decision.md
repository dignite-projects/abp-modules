# Decision: blob-storing is pipeline contributors for ABP BlobStoring; there is no upload service

**Status:** accepted, 2026-10-11 · **Applies from:** the next release (`10.0.0-rc.28`) · **Supersedes:**
[`core-only-decision.md`](core-only-decision.md) on what the module keeps

## Decision

`blob-storing/` (was `file-storing/`) ships **everyday `IBlobPipelineContributor` implementations for ABP BlobStoring**
and nothing in front of `IBlobContainer`:

- `Dignite.Abp.BlobStoring.Pipeline` — the `MaxSize`, `AllowedContentTypes` and `GZip` contributors, `IMimeTypeDetector`
  (content-based MIME detection) and `BlobStreamBuffering`.
- `Dignite.Abp.BlobStoring.Imaging` — the `ImageResize` and `ImageCompress` contributors and the image decode guard, on
  ABP's provider-agnostic imaging abstractions.

No upload entry point, no return object, no blob naming, no metadata. Those belong to the calling application: a caller
that needs the detected MIME type or a hash for its own metadata row calls `IMimeTypeDetector` and hashes itself, names
the blob itself, saves with `overrideExisting: false`, and deletes the blob if its metadata write fails. The old
`IFileHandler` pipeline, its runner `IFileStorer` and `StoredFileInfo`, `IBlobNameGenerator`, `ContainerNameValidator`,
`FileSizeLimitHandler`, `FileTypeCheckHandler` and `ImageResizeHandler` are removed.

## What changed: ABP 10.7.0 added the mechanism

ABP 10.7.0 introduced the BLOB content pipeline: `IBlobPipelineContributor` with `OnSavingAsync` / `OnGettingAsync`, a
per-container `BlobContainerConfiguration.PipelineContributors` list, and a `BlobPipelineContext` (container and blob
name, the configuration, the tenant, the cancellation token, the stream to replace). Contributors run inside
`IBlobContainer.SaveAsync` / `GetAsync`, in configuration order on save and in reverse on get; the built-in encryption always
runs after them on save. It is the same idea as this module's `IFileHandler` — a per-container ordered list of steps over a
stream — so keeping both would have meant two pipelines, one of them ours.

ABP shipped the mechanism and **no contributors**: only test fakes, with GZip and watermarking as documentation examples.
What an application still has to write itself is exactly what this module already had.

| Was (`Dignite.Abp.FileStoring`) | Now | Where |
|---|---|---|
| `IFileHandler` / `FileHandlerContext` | ABP's `IBlobPipelineContributor` / `BlobPipelineContext` | ABP |
| `IFileStorer` / `FileStorer`, `StoredFileInfo` | removed; the caller uses `IBlobContainer` | the application |
| `FileSizeLimitHandler` (`MaxFileSize`, MB) | `MaxSizeContributor` (`MaxSizeInBytes`) | Pipeline |
| `FileTypeCheckHandler` (`AllowedFileTypeNames`, extensions) | `AllowedContentTypesContributor` (`AllowedContentTypes`, MIME types matched against the content) | Pipeline |
| `ImageResizeHandler` (resize, minimum, limits, compress) | `ImageResizeContributor`, `ImageCompressContributor`, `ImageDecodeGuardConfiguration` | Imaging |
| `IMimeTypeDetector` | kept; `fileName` is now optional | Pipeline |
| — | `GZipContributor`, `BlobStreamBuffering` | Pipeline |
| `IBlobNameGenerator` / `RandomBlobNameGenerator` / `SetBlobNameGenerator<T>()` | removed; the caller names its blobs | the application |
| `ContainerNameValidator` | removed; ABP resolves a container's configuration, authorization is the application's | — |
| SHA-256 hash, size, detected MIME type in `StoredFileInfo` | removed; the caller computes what its row needs | the application |
| Compensating delete after a failed save | removed; the caller deletes the blob when its own metadata write fails | the application |

## Why not keep a thin wrapper (option B)

Option B kept a slimmed `IFileStorer` in front of the container: copy under a cap, detect, hand to the container, report
`StoredFileInfo`. It lost for two reasons.

1. **Without the handler pipeline, the wrapper was only "call five things in order".** Cap, detect, hash, name, save: each
   is a few lines, and each is the application's own decision (what cap, what name, which hash, what to record).
   Wrapping them bought a type name, not behaviour.
2. **It kept a parallel entry point next to `IBlobContainer`, exactly where ABP had just chosen to put behaviour on the
   container instead.** An application that injects `IBlobContainer` — or a module that writes blobs for its own reasons —
   would bypass the wrapper and its rules; the contributors apply to every write.

The cost of option A is what moved to the caller (below). The guidance for it is a short code example in the README.

## What moved to the caller

- **Naming.** The application chooses blob names (typically a GUID), and saves with `overrideExisting: false` so a collision
  is a `BlobAlreadyExistsException` rather than an overwrite — never "compensated" by deleting the existing blob.
- **The detected type and the hash.** An application whose metadata row records them calls `IMimeTypeDetector.DetectAsync`
  with the original file name and hashes the bytes, from one capped, seekable copy (`BlobStreamBuffering.CopyToBufferAsync`).
  These describe the upload; after a resize or GZip they no longer describe the stored bytes, which `StoredFileInfo` used to.
- **Consistency between blob and row.** Store the blob first, write the row, delete the blob if the row fails. No outbox.
- **Dedup, authorization, tenant rules beyond BlobStoring's own** — as before.

## The rename, and why the repository's invariant allows it

The packages are now `Dignite.Abp.BlobStoring.Pipeline` and `Dignite.Abp.BlobStoring.Imaging` (they were
`Dignite.Abp.FileStoring` and `Dignite.Abp.FileStoring.Imaging`), the folder is `blob-storing/`, and the root namespaces
follow.

Invariant 1 of the repository (root `CLAUDE.md`) forbids renaming a package to match a folder layout, and allows exactly one
kind of rename: adopting an ABP naming convention while the module is still pre-release, as `10.0.0-rc.24` did for the
notifications packages. This is that case, not a new precedent:

- The module is pre-stable (`10.0.0-rc.x`); no stable release has promised these IDs.
- `Dignite.Abp.BlobStoring.<Addition>` mirrors `Volo.Abp.BlobStoring.<Provider>` — what ABP calls a package that adds to its
  BlobStoring.
- "FileStoring" named the parallel pipeline (`IFileHandler`, `IFileStorer`) that no longer exists. The old name would
  describe something that was deleted.

The same breaking release removes the old types, so a Migrate section in the CHANGELOG maps every old name to its
replacement. After the first stable release the exception is gone.

## The image library

ABP 10.7 has three imaging providers behind `Volo.Abp.Imaging.Abstractions`: `Volo.Abp.Imaging.ImageSharp`, `.SkiaSharp` and
`.MagickNet`. `Dignite.Abp.BlobStoring.Imaging` references **only the abstractions**; the application chooses the provider.

This matters now because the provider this module used to reference, ImageSharp, is the problem one. ABP 10.7's provider
resolves SixLabors.ImageSharp 3.1.11, which carries three High-severity advisories, and the fixed releases (3.2.0, and 4.1.3
for the 4.x line) need a Six Labors license key at build time — a decision no library should take on its consumers' behalf.
With the abstractions-only reference this repository no longer references ImageSharp at all, and the vulnerability gate lost
its allowlist entry. The Imaging tests run on SkiaSharp, which has no open advisories.

## Deferred and not planned

Deferred: **`ImageMetadataStrip`.** ABP's imaging abstraction has no operation for it and the providers differ — ImageSharp
and Magick.NET keep EXIF data when they re-encode an image, SkiaSharp drops it — so a contributor would have to be
provider-specific. Not built on a guess.

Candidates for a second batch: `Watermark`, `SvgSanitize`, `ImageFormatConvert`, a ClamAV scan (its own package, so the core
takes no scanner dependency), and `ImageMetadataStrip`.

Not planned:

- **Thumbnails, dedup and hash sidecars.** A contributor is a one-to-one stream transform: it receives one stream and
  produces one stream for the same blob. Producing a second blob is a use case for an application service.
- **PDF and Office document disarming.**
- **Audit events.**

## Consequences

- **Site** (media library) and **Campus Support** (ticket attachments) call `IBlobContainer` directly. Each configures the
  contributors it wants on its containers, and does its own detecting, hashing and naming, with its own metadata and
  authorization. Neither depends on `IFileStorer`, which no longer exists; Site's media library — which dedups on a content
  hash — computes it from the upload.
- Existing blobs are untouched: the one-way contributors (`MaxSize`, `AllowedContentTypes`, image) change nothing about what
  is stored, and a container that is not given GZip keeps reading as before. A container that adds GZip later must re-save
  its blobs (README, "Stored format").
- The image decode guard's defaults are 8192 × 8192 pixels and 50,000,000 pixels in total (about 200 MB of RGBA per decode in
  the worst case), the decompression ratio is 100 and the timeout 10 seconds. The earlier ports of the old module's values
  (4096 and 16 MP) rejected ordinary 24 MP camera originals. The timeout only interrupts a provider that observes the
  cancellation token while decoding (ImageSharp does; SkiaSharp and Magick.NET decode synchronously), so for those the
  header-based dimension guard and the byte cap are the protections that apply.
- This module's invariants shrink to the contributor ones: ABP's stream-ownership contract, validators that never pass an
  unvalidated stream through, every buffering path capped, content deciding the type, and the one-way versus stored-format
  distinction (`.claude/skills/blob-storing-invariants`).
