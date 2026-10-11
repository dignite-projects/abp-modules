# Dignite.Abp.BlobStoring

Everyday `IBlobPipelineContributor` implementations for **ABP BlobStoring** (LGPL-3.0-only). ABP 10.7 ships the
BLOB content pipeline — `IBlobPipelineContributor`, per-container `BlobContainerConfiguration.PipelineContributors`,
run inside `IBlobContainer.SaveAsync` / `GetAsync` — but no contributors. This module is those: a size cap, a
content-type allow-list based on the real bytes, GZip, image resize and compress, and the MIME detector they share.
Published: `src/Dignite.Abp.BlobStoring.Pipeline` and `src/Dignite.Abp.BlobStoring.Imaging`. Nothing else lives here.

It was `Dignite.Abp.FileStoring` (an `IFileHandler` pipeline run by `IFileStorer`) until `10.0.0-rc.27`. ABP's pipeline
is the same idea, so the old pipeline and its runner were deleted and the packages renamed to follow ABP's naming
(`Dignite.Abp.BlobStoring.<Addition>`, as `Volo.Abp.BlobStoring.<Provider>`). Why, and the option that lost:
[`docs/pipeline-contributors-decision.md`](./docs/pipeline-contributors-decision.md). Before that, File Explorer left in
`10.0.0-rc.25`: [`docs/core-only-decision.md`](./docs/core-only-decision.md) (historical).

## What stays out

A contributor is a stream transform or check that runs inside the container. These are deliberately **not** here, and a
change that adds one is a design change, not a feature — re-read the decision document first:

- **No upload entry point.** No `IFileStorer`-style service in front of `IBlobContainer`; callers use the container.
- **No return object.** Nothing like `StoredFileInfo` — a contributor cannot report values back to the caller.
- **No blob naming.** The caller names its blobs.
- **No metadata, persistence, directories, dedup records.** A caller that wants the detected MIME type or a hash for
  its own row calls `IMimeTypeDetector` and hashes itself.
- **No HTTP API, no UI, no permissions.** Who may read or write a container is the application's.
- **No event bus, outbox or audit events.**
- **Nothing that is not a one-to-one stream transform or check.** A contributor cannot produce a second blob, so
  thumbnails, dedup or hash sidecars do not fit.

## Structure

One solution, `Dignite.Abp.BlobStoring.slnx`, central package management (versions in the root
`Directory.Packages.props`), no demo host:

- **`src/Dignite.Abp.BlobStoring.Pipeline`** — `MaxSizeContributor`, `AllowedContentTypesContributor`,
  `GZipContributor`, their `*Configuration` classes, `BlobContainerConfigurationExtensions` (`Add…Contributor`),
  `IMimeTypeDetector` / `MimeTypeDetector`, `BlobStreamBuffering`, `BlobStoringPipelineConsts`,
  `BlobStoringPipelineConfigurationNames`, `BlobStoringPipelineErrorCodes`, localization. Depends on
  `Volo.Abp.BlobStoring` and FileSignatures.
- **`src/Dignite.Abp.BlobStoring.Imaging`** — `ImageResizeContributor`, `ImageCompressContributor` (both on
  `ImageContributorBase`), their configurations, `ImageDecodeGuardConfiguration`, `ImageHeaderReader` (internal),
  `ImagingBlobContainerConfigurationExtensions`, `BlobStoringImagingErrorCodes`, localization. Depends on the Pipeline
  package and `Volo.Abp.Imaging.Abstractions` — **no image library**; the application picks one of ABP's providers.
- **`test/Dignite.Abp.BlobStoring.Pipeline.Tests`** and **`test/Dignite.Abp.BlobStoring.Imaging.Tests`** — integration
  tests on `Volo.Abp.BlobStoring.Memory`. The Imaging tests run on `Volo.Abp.Imaging.SkiaSharp` (plus
  `SkiaSharp.NativeAssets.Linux` for Linux CI) and on scripted fakes of `IImageResizer` / `IImageCompressor`.

Files are namespace-mirrored: `<Project>/<namespace path>/File.cs`, `<RootNamespace/>` empty. PackageId follows
AssemblyName. This folder's `README.md` is packed into both nupkgs as the package README, so it must stand alone for a
NuGet visitor — no links that only make sense inside the repository beyond the header blockquote.

## The rules the contributors must honour

ABP's `IBlobPipelineContributor` documents the stream-ownership contract. Quoted from the 10.7.0 source:

> Transform the content by replacing `BlobPipelineContext.BlobStream`: with a lazily transforming read-only wrapper (best
> for large content), or with an eagerly materialized stream. A replacement must leave the stream it received open:
> every stream **assigned** to `BlobPipelineContext.BlobStream` is disposed after the save, while the original stream
> stays owned by the caller. A stream is only tracked from the moment it is assigned, so if you create a stream and then
> do work that may fail before assigning it, dispose it yourself on the failure path.
>
> Not replacing the stream is only valid for a contributor that does not consume the content (for example a metadata
> check). A contributor that reads the content to validate it must return a pass-through wrapper that validates the bytes
> as they flow (or an eagerly materialized replacement) — reading the content without replacing the stream would leave an
> empty/truncated stream for the provider.

And for reading (`OnGettingAsync`): *"a replacement must dispose the stream it received when it is disposed, since the
composed stream is returned to the caller as a whole."* Contributors run in configuration order on save and in reverse on
get; ABP's built-in encryption always runs after them on save and before them on get.

What that means here: a stream created and not yet assigned is disposed by the contributor on the failure path
(`MaxSizeContributor`, `GZipContributor`); the validating contributors either probe a seekable stream in place and rewind
it, or replace it with a capped buffered copy (`BlobStreamBuffering.EnsureSeekableAsync`); a decompressing wrapper
disposes what it wraps. Read the `blob-storing-invariants` skill before touching any of this.

## Adding a contributor

1. The contributor class in `Dignite.Abp.BlobStoring.Pipeline` (or `.Imaging` if it needs imaging), `ITransientDependency`.
   It is a plain class: inject what it needs. Use `context.CancellationToken` for every I/O call. Validators that consume
   the content use `BlobStreamBuffering`; transforms replace `context.BlobStream` and leave the received stream open.
2. `*ContributorConfiguration`: a wrapper over `BlobContainerConfiguration` whose properties read and write named entries
   (`GetConfigurationOrDefault` / `SetConfiguration`), and a `Validate()` that throws `AbpException`. Entry names go in
   `BlobStoringPipelineConfigurationNames` / `BlobStoringImagingConfigurationNames`.
3. An `Add…Contributor(this BlobContainerConfiguration, Action<…Configuration>)` extension in
   `BlobContainerConfigurationExtensions` (Pipeline) or `ImagingBlobContainerConfigurationExtensions` (Imaging — a
   different class name so a file importing both namespaces can still name either). It applies the configure action,
   calls `Validate()`, then `containerConfiguration.PipelineContributors.TryAdd<TContributor>()`, so the configured order
   is the execution order and a contributor is added once. A `Get…ContributorConfiguration()` reader sits next to it.
   The contributor calls `Validate()` again at the start of `OnSavingAsync`.
4. An error code for each new failure, in the package's `…ErrorCodes` class, and a key for it in **all four** resource
   files (`Localization/Resources/{en,ja,zh-Hans,zh-Hant}.json`). Data placeholders are `{Name}`, filled with
   `.WithData("Name", value)`.
5. Tests in the package's test project on `Volo.Abp.BlobStoring.Memory`: register the container in the test module, save
   through `IBlobContainer`, read the stored bytes straight from the shared `MemoryBlobProvider`. Cover the failure paths
   (nothing stored, streams disposed), cancellation, the stored-format behaviour on read, and a composed container.

A new image operation reuses `ImageContributorBase` (seekable copy, type detection, decode guard, timeout, result
mapping). A new MIME format extends `MimeTypeDetector`'s protected virtual members, or is added to the default signature
list when it is generally useful.

## Error codes

Two namespaces, because ABP maps a code namespace to exactly one localization resource and each package has its own
resource: `Dignite.Abp.BlobStoring:…` (`BlobStoringPipelineErrorCodes`, resource `BlobStoringPipelineResource`, mapped in
`DigniteAbpBlobStoringPipelineModule`) and `Dignite.Abp.BlobStoring.Imaging:…` (`BlobStoringImagingErrorCodes`, resource
`BlobStoringImagingResource`, mapped in `DigniteAbpBlobStoringImagingModule`). Do not reuse the Pipeline namespace in the
Imaging package, and do not add a third namespace without a third resource.

## `BlobStreamBuffering`

Public, because contributors in other packages (Imaging, and any a consumer writes) need the same capped buffering:

- `EnsureSeekableAsync(context, maxBytes)` returns `context.BlobStream` when it is seekable and at position 0, otherwise
  copies it from its current position into memory (failing beyond `maxBytes`) and assigns the copy to
  `context.BlobStream`, so the pipeline owns and disposes it.
- `CopyToBufferAsync(source, maxBytes, cancellationToken)` copies into memory, failing with `ContentTooLarge` as soon as
  more than `maxBytes` have been read — and before reading anything when a seekable source already reports a larger
  remaining length. It leaves `source` open, disposes its own buffer on failure and returns the buffer at position 0.
- `CreateContentTooLargeException(maxBytes)` builds the `ContentTooLarge` exception with `MaxSizeInBytes` data.

Every buffering path is capped: the container's `MaxSizeInBytes`, or `BlobStoringPipelineConsts.DefaultMaxBufferedBytes`
(100 MB) when it has none.

## Commands

```bash
dotnet build blob-storing/Dignite.Abp.BlobStoring.slnx -c Release
dotnet test blob-storing/Dignite.Abp.BlobStoring.slnx -c Release
dotnet pack blob-storing/Dignite.Abp.BlobStoring.slnx -c Release
```

## Skills

Both load automatically for work under this folder:

- [`blob-storing-conventions`](./.claude/skills/blob-storing-conventions/SKILL.md) — how this module applies ABP: the
  contributor + configuration + extension pattern, DI lifetimes, error codes and localization, the detector as a
  replaceable service, test conventions.
- [`blob-storing-invariants`](./.claude/skills/blob-storing-invariants/SKILL.md) — what a change must not break. Read it
  before touching streams, buffering, MIME detection, an image contributor or a stored format.
