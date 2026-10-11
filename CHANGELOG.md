# Changelog

All notable changes to the packages released from this repository — the `blob-storing/`,
`notifications/` and `flex-fields/` modules, and the shared `aspnetcore-mcp/` tree — are documented in
this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) — with two deviations from the
classic scheme: every package in the repository shares **one lockstep version**, and `MAJOR` tracks
the targeted **ABP Framework** version rather than this repository's own breaking changes. See
[CONTRIBUTING.md → Versioning and releases](CONTRIBUTING.md#versioning-and-releases) for both.

Because releases are lockstep, a version may contain changes to only one module — the other modules'
packages are still republished at that version with unchanged content. Entries are grouped by module
so it stays clear which part of the repository actually moved.

## [Unreleased]

### Added

#### blob-storing (was file-storing)

- **`Dignite.Abp.BlobStoring.Pipeline`** (`DigniteAbpBlobStoringPipelineModule`, namespace `Dignite.Abp.BlobStoring.Pipeline`):
  everyday `IBlobPipelineContributor`s for the BLOB content pipeline ABP 10.7.0 introduced. ABP ships the mechanism
  (`IBlobPipelineContributor`, `BlobContainerConfiguration.PipelineContributors`, run inside `IBlobContainer.SaveAsync` /
  `GetAsync`, in configuration order on save and in reverse on get) and no contributors; these are the ones that were
  `IFileHandler`s before, and GZip. Depends on `Volo.Abp.BlobStoring` and FileSignatures (MIT).
  - `MaxSizeContributor` - `AddMaxSizeContributor(c => c.MaxSizeInBytes = ...)`. Rejects larger content with
    `Dignite.Abp.BlobStoring:ContentTooLarge`, while the content is read: a seekable stream whose length is over the limit
    is rejected without reading, any other stream as soon as the copy passes it. The copy replaces the stream, so later
    contributors get a seekable stream with a known length.
  - `AllowedContentTypesContributor` - `AddAllowedContentTypesContributor(c => c.AllowedContentTypes = [...])`, with
    `AllowUnidentified`. Rejects content whose type, detected from the bytes, is not on the list
    (`Dignite.Abp.BlobStoring:ContentTypeNotAllowed`); entries are exact types or `type/*` wildcards.
  - `GZipContributor` - `AddGZipContributor(c => c.CompressionLevel = ...)`. Stores the content GZip-compressed and
    decompresses it on read. It is part of the stored format: do not add it to, or remove it from, a container that
    already has blobs.
  - `IMimeTypeDetector` / `MimeTypeDetector`: the MIME type from the content's signature (FileSignatures, which also
    looks inside archives, so an Office Open XML or OpenDocument file is not a plain ZIP), a text sniff for HTML, SVG and
    XML, and a policy that reconciles the result with the extension (`Dignite.Abp.BlobStoring:ContentTypeMismatch`).
    Replaceable with `[Dependency(ReplaceServices = true)]`. The README states what it does not protect against.
  - `BlobStreamBuffering` (public): capped buffering for contributors that need to re-read the content;
    `BlobStoringPipelineConsts.DefaultMaxBufferedBytes` (100 MB) bounds it when a container has no `MaxSize`.
- **`Dignite.Abp.BlobStoring.Imaging`** (`DigniteAbpBlobStoringImagingModule`, namespace `Dignite.Abp.BlobStoring.Imaging`):
  image contributors on ABP's provider-agnostic `Volo.Abp.Imaging.Abstractions`. It references no image library; the
  application adds one of ABP's providers (`Volo.Abp.Imaging.SkiaSharp`, `.ImageSharp` or `.MagickNet`).
  - `ImageResizeContributor` - `AddImageResizeContributor(c => ...)` with `MaxWidth`, `MaxHeight`, `MinWidth`,
    `MinHeight` and `Mode`. An image is resized only when its header dimensions exceed the box (an image that fits is
    stored byte for byte, never upscaled); a smaller source is rejected with
    `Dignite.Abp.BlobStoring.Imaging:ImageTooSmall`. Content that is not an image, and an image the provider does not
    support, pass through.
  - `ImageCompressContributor` - `AddImageCompressContributor()`. Re-encodes with ABP's `IImageCompressor`, keeping the
    original when the result is not smaller.
  - The image decode guard - `ConfigureImageDecodeGuard(g => ...)`, applied by both contributors before the provider
    decodes anything: `MaxSourceWidth` (8192), `MaxSourceHeight` (8192), `MaxSourcePixels` (50,000,000; about 200 MB of
    RGBA per decode in the worst case), `MaxDecompressionRatio` (100 pixels per stored byte) and `DecodeTimeout` (10 s).
    Dimensions are read from the header (PNG, JPEG, GIF, BMP, WebP, TIFF) without decoding. The timeout only
    interrupts a provider that observes the cancellation token while decoding (ImageSharp does; SkiaSharp and Magick.NET
    decode synchronously), so for those the dimension guard and the byte cap are the protections that apply.
  - Both contributors are one-way transforms, so they can be added to a container that already has blobs.
- Error codes are named, not numbered: `Dignite.Abp.BlobStoring:ContentTooLarge` / `ContentTypeMismatch` /
  `ContentTypeNotAllowed`, and `Dignite.Abp.BlobStoring.Imaging:ImageTooLarge` / `ImageTooSmall` / `ImageDecodeTimeout` /
  `ImageProcessingFailed`, each localized in English, Japanese, Simplified and Traditional Chinese. See the module's
  README for the contributor reference and
  [`blob-storing/docs/pipeline-contributors-decision.md`](blob-storing/docs/pipeline-contributors-decision.md) for why
  this replaces the old pipeline.

### Changed

- **Breaking - every NuGet package now targets ABP Framework 10.7.0 (was 10.5.0).** All `Volo.Abp.*` references in
  the root `Directory.Packages.props` and in both demo hosts moved to 10.7.0, so the packages' published
  dependencies are now `Volo.Abp.* >= 10.7.0`: a consuming host must be on ABP 10.7.0 or later. No source change was
  needed to compile against it. The Angular packages are not part of this: they stay on `@abp/ng.*` ~10.5.0 and
  Angular 21, because ABP 10.6 moved its Angular packages to Angular 22 and that upgrade has not been decided.
- **The repository-wide security pins are gone, because ABP 10.7.0 resolves a patched version of each by itself.**
  `SQLitePCLRaw.*` 2.1.12 (GHSA-2m69-gcr7-jv3q): `Volo.Abp.EntityFrameworkCore.Sqlite` 10.7.0 brings
  `Microsoft.EntityFrameworkCore.Sqlite` 10.0.11, which depends on 2.1.12. Scriban 7.2.7 (GHSA-7jvp-hj45-2f2m, fixed in
  7.2.2): `Volo.Abp.TextTemplating.Scriban` 10.7.0 depends on Scriban 7.2.5. `Polly` stays at 8.6.3, still the version
  `Volo.Abp.PermissionManagement.Domain` 10.7.0 uses. The vulnerability gate in `ci.yml` / `release.yml` no longer
  allowlists GHSA-7jvp-hj45-2f2m, which no resolved Scriban is affected by any more.
- Demo hosts (`notifications/host/`, `flex-fields/demo/`; never published): `Volo.Abp.Studio.Client.AspNetCore`
  3.0.8 → 3.1.4, the newest release whose minimum `Volo.Abp.AspNetCore` (10.6.1) does not exceed 10.7.0 - none
  requires exactly 10.7.0 yet - and dependabot now ignores it from 3.1.5 on, until a release's ABP requirement is
  checked. LeptonX Lite 5.5.0 → 5.7.0, its release for ABP 10.7. Their inline security overrides are dropped for
  the same reason as the repository pins: MessagePack (Studio.Client 3.1.4 → MagicOnion 7.10.2 → 3.1.7),
  Microsoft.OpenApi (`Volo.Abp.Swashbuckle` 10.7.0 → Swashbuckle.AspNetCore 10.2.3 → 2.7.5), Scriban 7.2.5 and
  SQLitePCLRaw 2.1.12 are all past their advisories. Dependabot keeps ignoring Microsoft.OpenApi 3.x: Swashbuckle
  10.2.3 is still built against 2.x.
- **The vulnerability gate in `ci.yml` / `release.yml` now checks every advisory line.** Its regex only inspected the
  first advisory line of each package (and never matched a top-level package's row), so the three High
  SixLabors.ImageSharp 3.1.11 advisories reaching the earlier Imaging package through `Volo.Abp.Imaging.ImageSharp` 10.7.0
  went unseen. They are not allowlisted: the fixed releases (3.2.0, 4.1.3) require a Six Labors license key to build, so the
  package that reached them was replaced instead - `Dignite.Abp.BlobStoring.Imaging` (see blob-storing below) references only
  ABP's imaging abstractions, and nothing in the solution references ImageSharp any more.

#### blob-storing (was file-storing)

- **Breaking - `file-storing/` is now `blob-storing/`, its packages are `Dignite.Abp.BlobStoring.*`, and the `IFileHandler`
  pipeline and `IFileStorer` are gone.** ABP 10.7.0's `IBlobPipelineContributor` is the same idea as `IFileHandler` (a
  per-container ordered list of steps over a stream), so the module keeps no pipeline of its own: it ships contributors for
  ABP's (see Added), and what used to wrap the container (`IFileStorer`, `StoredFileInfo`, `IBlobNameGenerator`) is now the
  calling application's. The module is pre-stable and the rename adopts ABP's own naming
  (`Dignite.Abp.BlobStoring.<Addition>`, as `Volo.Abp.BlobStoring.<Provider>`), so it falls under the one exception the
  repository's PackageId invariant allows - the same one `10.0.0-rc.24` used for the notifications packages - and is not a
  precedent for renaming after a layout change. Why, and the wrapper that lost:
  [`blob-storing/docs/pipeline-contributors-decision.md`](blob-storing/docs/pipeline-contributors-decision.md).
- The folder `file-storing/` is `blob-storing/`, and its solution is `blob-storing/Dignite.Abp.BlobStoring.slnx`, holding
  `src/Dignite.Abp.BlobStoring.Pipeline`, `src/Dignite.Abp.BlobStoring.Imaging` and a test project for each.
- Error codes: `Dignite.Abp.File:0001`-`0005` and `Dignite.Abp.FileStoring.Imaging:0001`-`0006` are replaced by the named
  codes under `Dignite.Abp.BlobStoring` and `Dignite.Abp.BlobStoring.Imaging` (table below). The Imaging package has a
  namespace of its own because ABP maps one code namespace to one localization resource.
- `IMimeTypeDetector.DetectAsync(Stream, string? fileName = null, CancellationToken = default)`: `fileName` is optional. A
  blob name without an extension gets no extension reconciliation; the content alone decides the type.
- The image decode guard's defaults are 8192 x 8192 pixels and 50,000,000 pixels in total (the handler's were 4096 x 4096
  and 16,000,000), because the old values rejected ordinary 24 MP camera originals.

  > **Migrate:** the old types are removed, not obsoleted. Update the packages, modules, namespaces and the container
  > configuration as below; code that called `IFileStorer` moves to the calling sequence at the end.
  >
  > | rc.27 | Now |
  > |---|---|
  > | package `Dignite.Abp.FileStoring`, `DigniteAbpFileStoringModule`, namespace `Dignite.Abp.FileStoring` | package `Dignite.Abp.BlobStoring.Pipeline`, `DigniteAbpBlobStoringPipelineModule`, namespace `Dignite.Abp.BlobStoring.Pipeline` |
  > | package `Dignite.Abp.FileStoring.Imaging`, `DigniteAbpFileStoringImagingModule`, namespace `Dignite.Abp.FileStoring.Imaging` | package `Dignite.Abp.BlobStoring.Imaging`, `DigniteAbpBlobStoringImagingModule`, namespace `Dignite.Abp.BlobStoring.Imaging`; **and add one of ABP's imaging providers explicitly** (`Volo.Abp.Imaging.SkiaSharp`, `.ImageSharp` or `.MagickNet`, with its module) - the package no longer brings ImageSharp |
  > | `AddFileSizeLimitHandler(h => h.MaxFileSize = 20)` (megabytes) | `AddMaxSizeContributor(c => c.MaxSizeInBytes = 20 * 1024 * 1024)` (bytes) |
  > | `AddFileTypeCheckHandler(h => h.AllowedFileTypeNames = [".pdf"])` (extensions) | `AddAllowedContentTypesContributor(c => c.AllowedContentTypes = ["application/pdf"])` - **content types now, matched against the detected content, not file extensions** |
  > | `AddImageResizeHandler(h => { h.ImageWidth = w; h.ImageHeight = h; h.ImageSizeMustBeLargerThanPreset = true; h.MaxImageWidth / MaxImageHeight / MaxPixelCount / MaxDecompressionRatio / DecodeTimeoutSeconds })` | `AddImageResizeContributor(c => { c.MaxWidth = w; c.MaxHeight = h; c.MinWidth = w; c.MinHeight = h; })` (the minimums only where `ImageSizeMustBeLargerThanPreset` was on) + `AddImageCompressContributor()` (the handler compressed after resizing) + `ConfigureImageDecodeGuard(g => { g.MaxSourceWidth; g.MaxSourceHeight; g.MaxSourcePixels; g.MaxDecompressionRatio; g.DecodeTimeout = TimeSpan.FromSeconds(n) })` |
  > | `IFileStorer.StoreAsync` / `DeleteAsync` | `IBlobContainer.SaveAsync` / `DeleteAsync`, with the caller-side sequence below |
  > | `StoredFileInfo` (`BlobName`, `Size`, `MimeType`, `Hash`) | the caller computes what its row needs: `IMimeTypeDetector.DetectAsync`, SHA-256, the length - of the *upload*, not of the bytes after a resize or GZip |
  > | `IBlobNameGenerator`, `RandomBlobNameGenerator`, `SetBlobNameGenerator<T>()` | the caller names its blobs (a GUID), and saves with `overrideExisting: false` |
  > | `ContainerNameValidator`, `FileConsts`, `ImageFormatHelper`, `FileHandlerContext` | removed |
  > | `Dignite.Abp.File:0001` (`FileTooLarge`) | `Dignite.Abp.BlobStoring:ContentTooLarge` |
  > | `Dignite.Abp.File:0002` (`InvalidImageType`) | `Dignite.Abp.BlobStoring:ContentTypeNotAllowed` |
  > | `Dignite.Abp.File:0003` (`MissingFileExtension`), `0004` (`Containers.NotFound`) | no replacement: an extension is no longer required, and there is no container-name check |
  > | `Dignite.Abp.File:0005` (`ContentTypeMismatch`) | `Dignite.Abp.BlobStoring:ContentTypeMismatch` |
  > | `Dignite.Abp.FileStoring.Imaging:0001` (`ImageSizeTooSmall`) | `Dignite.Abp.BlobStoring.Imaging:ImageTooSmall` |
  > | `...Imaging:0002` (`ImageResizeFailure`) | `Dignite.Abp.BlobStoring.Imaging:ImageProcessingFailed` |
  > | `...Imaging:0003` (`ImageTooLarge`) | `Dignite.Abp.BlobStoring.Imaging:ImageTooLarge` |
  > | `...Imaging:0004` (`ImageFormatNotSupported`), `0006` (`ImageResizeDimensionsTooLarge`) | no replacement: an image the provider does not support passes through unchanged; to forbid it, list the allowed types in `AddAllowedContentTypesContributor` |
  > | `...Imaging:0005` (`ImageDecodeTimeout`) | `Dignite.Abp.BlobStoring.Imaging:ImageDecodeTimeout` |
  >
  > Behaviour that changes with it:
  > - **A container without `AddMaxSizeContributor` has no size limit** (`IFileStorer` capped every upload at 100 MB,
  >   `FileConsts.DefaultMaxFileSizeInBytes`). Add `AddMaxSizeContributor` to every container that takes uploads.
  > - Put the contributors in the recommended order: `MaxSize`, `AllowedContentTypes`, image contributors, GZip.
  > - Nothing deletes a partially written blob after a failed save any more; deleting the blob when your own metadata
  >   write fails is still yours to do.
  >
  > The calling sequence that replaces `IFileStorer.StoreAsync` (see the README for the explanation):
  >
  > ```csharp
  > await using var content = await BlobStreamBuffering.CopyToBufferAsync(upload, maxBytes, cancellationToken);
  > var mimeType = await _mimeTypeDetector.DetectAsync(content, fileName, cancellationToken);
  > var hash = Convert.ToHexString(await SHA256.HashDataAsync(content, cancellationToken));
  > content.Position = 0;
  > var blobName = GuidGenerator.Create().ToString("N");
  > await _container.SaveAsync(blobName, content, overrideExisting: false, cancellationToken);   // the contributors run here
  > try { /* insert your metadata row: blobName, fileName, mimeType, content.Length, hash */ }
  > catch { await _container.DeleteAsync(blobName, CancellationToken.None); throw; }
  > ```

#### notifications

- `Dignite.Abp.Notifications.Emailing` no longer lists Scriban as a dependency of its own. The Scriban pin had put
  `Scriban >= 7.2.7` into its published dependency list; consumers now get 7.2.5 through `Volo.Abp.Emailing` 10.7.0.
- ABP 10.7's `GdprUserDataDeletionRequestedEto` carries a `TenantId`, and when the publisher sets it the event bus
  runs `GdprUserDataDeletionRequestedHandler` inside that tenant. The handler is unchanged: it still deletes the
  user's rows by user id in every tenant sharing the database, so it is not narrowed to the event's tenant.
- `Dignite.Abp.Notifications.Emailing.Identity` and `Dignite.NotificationCenter.Push.Identity` depend on
  `AbpIdentityDomainModule`, which in ABP 10.7 registers the Identity token providers and calls
  `AddDataProtection()`. A process loading either module now loads a Data Protection key ring at startup (and creates
  one when the store is empty), even if it never issues an Identity token.

#### flex-fields

- The kernel maps no relationship between a host entity and its `FlexFieldIndexBase<TEntity>` rows; a host that adds
  one without a navigation on the host entity - the demo's `HasOne<Product>().WithMany()` for
  `ProductFlexFieldIndex` - sees ABP 10.7 stop updating the tracked host entity (concurrency stamp, modification
  audit, entity updated event) when only its index rows change. No change here; the demo is left as is.

### Removed

#### blob-storing (was file-storing)

- The NuGet packages `Dignite.Abp.FileStoring` and `Dignite.Abp.FileStoring.Imaging` are no longer published, starting with
  this version. Their replacements are `Dignite.Abp.BlobStoring.Pipeline` and `Dignite.Abp.BlobStoring.Imaging` (see
  Changed for the mapping). The IDs are retired, not reused for something else.
- The pipeline and its runner: `IFileHandler`, `FileHandlerContext`, `IFileStorer` / `FileStorer` and its typed-container
  extensions, `StoredFileInfo`.
- `IBlobNameGenerator`, `RandomBlobNameGenerator`, `SetBlobNameGenerator<T>()` / `GetBlobNameGeneratorType()` and
  `BlobContainerConfigurationNames.BlobNameGenerator`; `ContainerNameValidator`.
- The handlers and their configuration: `FileSizeLimitHandler`, `FileTypeCheckHandler`, `ImageResizeHandler`, with their
  `*Configuration` / `*ConfigurationNames` and `AddFileSizeLimitHandler` / `AddFileTypeCheckHandler` /
  `AddImageResizeHandler`.
- `FileConsts`, `FileErrorCodes`, `FileStoringImagingErrorCodes`, `ImageFormatHelper`, `FileStoringResource`.

## [10.0.0-rc.27] - 2026-10-10

### Added

#### flex-fields

- `CKEDITOR_DISPLAY_CONTRIBUTORS` / `CKEditorDisplayContributor` (`@dignite/ng.flex-fields-ckeditor`): a multi provider
  through which the host rewrites the HTML the read-only view (`ff-ckeditor-view`) displays, the counterpart of
  `CKEDITOR_CONFIG_CONTRIBUTORS` for display. A contributor gets the HTML (a Markdown value already converted) and a
  context (the field and its content format) and returns the HTML to show; contributors run in registration order,
  once whenever the view's value or field changes, before Angular's sanitizer. With none registered nothing changes.
  See the package README for an example that prefixes stored relative image addresses with the API host.

## [10.0.0-rc.26] - 2026-10-10

### Added

#### flex-fields

- `CKEDITOR_CONFIG_CONTRIBUTORS` / `CKEditorConfigContributor` (`@dignite/ng.flex-fields-ckeditor`): a multi provider
  through which the host changes the CKEditor 5 configuration each `CKEditor` field's editor is created with — extra
  plugins, toolbar, any other `EditorConfig` option. A contributor gets the composed configuration and a context (the
  loaded `ckeditor5` package, the field, its mode and content format) and changes it in place or returns a
  replacement; contributors run in registration order, once per editor. With none registered nothing changes. See the
  package README for an example that shows stored relative image addresses from the API host while editing, keeping
  `getData()` relative.

## [10.0.0-rc.25] - 2026-10-10

### Added

#### file-storing

- `IFileStorer` (default `FileStorer`, transient) in `Dignite.Abp.FileStoring` — the runner of a container's
  `IFileHandler` pipeline, which until now existed only inside File Explorer's `FileDescriptorManager`.
  `StoreAsync(containerName, fileName, stream, cancellationToken)` validates the container name, copies the upload
  into a buffer capped at the container's size limit, detects the MIME type from the content, runs the handlers in
  their configured order, hashes the result with SHA-256, names it with the container's `IBlobNameGenerator` and saves
  it without overwriting, deleting a partially written blob again when the save fails. It returns
  `StoredFileInfo { BlobName, Size, MimeType, Hash }`, all describing the stored (post-handler) bytes.
  `DeleteAsync(containerName, blobName, cancellationToken)` removes a blob. `StoreAsync<TContainer>` /
  `DeleteAsync<TContainer>` extensions take a typed container. Content dedup and file metadata stay with the caller:
  store first, write your row, and call `DeleteAsync` if the row fails. See
  [`file-storing/docs/core-only-decision.md`](file-storing/docs/core-only-decision.md).
- `IMimeTypeDetector` (default `MimeTypeDetector`): MIME type from the content's signature, reconciled with the file
  extension. A file whose content contradicts its extension (an executable named `.png`, text named `.pdf`) is
  rejected with the new error code `Dignite.Abp.File:0005`; formats without a signature (text, CSV, JSON, SVG) fall
  back to the extension, unknown ones to `application/octet-stream`.
- `SetBlobNameGenerator<T>()` / `GetBlobNameGeneratorType()` and `BlobContainerConfigurationNames.BlobNameGenerator`
  (same `"BlobNameGenerator"` key) now live in core; they were part of `Dignite.FileExplorer.Domain`.
- `FileConsts.DefaultMaxFileSizeInBytes` (100 MB): the cap `IFileStorer` applies to a container without
  `AddFileSizeLimitHandler`.
- `FileHandlerContext.CancellationToken`, the store operation's token, as on ABP's `BlobPipelineContext`.

#### flex-fields

- `CKEditorUploadProvider` / `CKEDITOR_UPLOAD_PROVIDER` (`@dignite/ng.flex-fields-ckeditor`): the host registers how an
  inline image is uploaded (`upload(file, containerName)` resolving to the URL to embed). The package ships no
  implementation.

### Changed

#### file-storing

- **The MIME type comes from the content.** Under `IFileStorer`, `FileHandlerContext.MimeType` and
  `StoredFileInfo.MimeType` are detected from the bytes; the API takes no caller-supplied MIME type at all, and
  `FileTypeCheckHandler`'s extension check runs only after the extension has been checked against the content.
- **The size limit binds while copying.** `IFileStorer` stops reading an upload as soon as it passes the container's
  limit (and rejects a seekable stream whose length is over the limit before reading it), instead of buffering it
  whole and checking afterwards.
- `ImageResizeHandler` links the operation's cancellation token into its decode timeout and passes it to ABP's image
  resizer and compressor. A caller's cancellation now surfaces as `OperationCanceledException`, not as the
  decode-timeout error.
- `FileHandlerContext`'s constructor takes an optional trailing `CancellationToken` — source compatible, but code
  compiled against rc.24 must be recompiled.

#### flex-fields

- **Breaking — the CKEditor image upload target is the host's.** The adapter no longer posts to
  `POST /api/file-explorer/files` with apiName `FileExplorer`; `CKEditorUploadAdapter` takes the host's
  `CKEditorUploadProvider`. Without a registered `CKEDITOR_UPLOAD_PROVIDER` the upload-image button is omitted, as it
  already was for a field with no `CKEditor.ImagesContainerName`.

  > **Migrate:** a host that relied on the File Explorer upload registers a provider for its own file API
  > (`{ provide: CKEDITOR_UPLOAD_PROVIDER, useExisting: MyUploadProvider }`); see the package README.

### Removed

#### file-storing

- **File Explorer has left this repository.** `file-storing/` is now only the enhancement layer on ABP BlobStoring
  (`Dignite.Abp.FileStoring`, `Dignite.Abp.FileStoring.Imaging`). These packages are no longer published from here,
  starting with this version:
  - NuGet: `Dignite.FileExplorer.Domain.Shared`, `.Domain`, `.Application.Contracts`, `.Application`, `.HttpApi`,
    `.HttpApi.Client`, `.EntityFrameworkCore`, `.MongoDB`, `.Mcp`, `.Installer`.
  - npm: `@dignite/ng.file-explorer`.

  The file browser is the CMS media library; it moves to the [`site`](https://github.com/dignite-projects/site)
  repository as a Site feature, under the Site namespace (a new package identity, not a continuation of these IDs).
  An application that only needs files stored safely (attachments, uploads tied to its own entities) uses
  `IFileStorer` from core and keeps its own metadata. The demo host (`Dignite.FileExplorer.Web.Host`) and the
  module's Angular workspace are gone with it; the module's solution is `file-storing/Dignite.Abp.FileStoring.slnx`.

#### flex-fields

- The FileExplorer field type: NuGet `Dignite.Abp.FlexFields.FileExplorer` and `Dignite.Abp.FlexFields.FileExplorer.Web`,
  npm `@dignite/ng.flex-fields-file-explorer`. A media-picker field belongs with the media library and moves to the
  `site` repository with it. The flex-fields demo no longer seeds an `images` field.

## [10.0.0-rc.24] - 2026-10-09

### Added

#### notifications

- `NullNotificationPublisher` (Abstractions), the null default of `INotificationPublisher`, after ABP's `NullSmsSender`.
- `NotificationDefinitionsChangedEto` (`Dignite.Abp.Notifications.Domain.Shared`, wire name
  `Dignite.Abp.Notifications.NotificationDefinitionsChanged`), after ABP's `DynamicPermissionDefinitionsChangedEto`:
  the saver publishes the names of the definitions a save inserted or changed, through the distributed event bus in
  its unit of work (into the outbox when one is configured). Group-only changes and deletions publish nothing, as in
  ABP. Nothing in the module handles it; the dynamic store still reloads through the common stamp.
- `Dignite.Abp.Notifications.Domain.Shared` (`netstandard2.0;netstandard2.1;net10.0`): the record column sizes
  (`NotificationDefinitionRecordConsts`, `NotificationGroupDefinitionRecordConsts`, moved from the definition store)
  and the event above.
- `Dignite.Abp.Notifications.MongoDB` (`AbpNotificationsMongoDbModule`, namespace `Dignite.Abp.Notifications.MongoDB`):
  the definition store on MongoDB, after `Volo.Abp.PermissionManagement.MongoDB` and `Volo.Abp.FeatureManagement.MongoDB`
  — the EF Core package's counterpart for hosts whose Notification Center runs on `Dignite.NotificationCenter.MongoDB`.
  `NotificationDefinitionStoreMongoDbContext` (`[IgnoreMultiTenancy]`, connection string `NotificationCenter`) keeps the
  records in the `NotifDefinitionGroups` / `NotifDefinitions` collections with the EF Core tables' indexes (a unique
  `Name` on both, `GroupName` on the definitions), and `ConfigureNotificationDefinitionStore()` maps them into a host's
  own MongoDB context. The saver's unit of work is transactional, as in ABP, so MongoDB must run as a replica set.

### Changed

#### notifications

- **Breaking - the notifications packages follow ABP's package layout.** One `.Abstractions` package holds every
  contract and its null default (as `Volo.Abp.Authorization.Abstractions` does), the in-process implementation takes
  the plain name (as `Volo.Abp.BackgroundJobs` does), remote publishing lives in a `.Client` package (as
  `RemotePermissionChecker` lives in `Volo.Abp.AspNetCore.Mvc.Client(.Common)`), and the definition store is the feature's
  `.Domain.Shared` / `.Domain` / `.EntityFrameworkCore` (as `Volo.Abp.PermissionManagement.Domain` is):

  | rc.23 package | rc.24 package | rc.23 module | rc.24 module |
  |---|---|---|---|
  | `Dignite.Abp.Notifications` (contracts) | `Dignite.Abp.Notifications.Abstractions` | `AbpNotificationsModule` | `AbpNotificationsAbstractionsModule` |
  | `Dignite.Abp.Notifications.Distribution` | `Dignite.Abp.Notifications` (implementation) | `AbpNotificationsDistributionModule` | `AbpNotificationsModule` |
  | `Dignite.Abp.Notifications.Remote` | `Dignite.Abp.Notifications.Client` | `AbpNotificationsRemoteModule` | `AbpNotificationsClientModule` |
  | `Dignite.Abp.Notifications.DefinitionStore` | `Dignite.Abp.Notifications.Domain` + `Dignite.Abp.Notifications.Domain.Shared` | `AbpNotificationsDefinitionStoreModule` | `AbpNotificationsDomainModule` (+ `AbpNotificationsDomainSharedModule`) |
  | `Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore` | `Dignite.Abp.Notifications.EntityFrameworkCore` | `AbpNotificationsDefinitionStoreEntityFrameworkCoreModule` | `AbpNotificationsEntityFrameworkCoreModule` |

  Everything Core held — the definition API and its static/dynamic stores, `NotificationRoutingOptions`, the channel
  resolver, `INotificationPublisher`, `INotificationStore`, `INotificationDistributor`, `INotificationPermissionChecker`,
  the info records, `NotificationEntityIdentifier`, the routing-name startup check — moved into Abstractions with the
  same `Dignite.Abp.Notifications` namespace, as did `NullNotificationStore` and `AlwaysGrantedNotificationPermissionChecker`
  from Distribution. `AbpNotificationsAbstractionsModule` now also depends on `AbpFeaturesModule` and runs Core's
  options validation. Abstractions keeps `netstandard2.0;netstandard2.1;net10.0`. The types moved assembly, so modules
  compiled against rc.23 must be recompiled.

  > **Migrate — business modules: `AbpNotificationsModule` changed meaning.** In rc.23 it was the contracts module a
  > business module depended on; in rc.24 it is the in-process implementation and brings the distributor, the
  > distribution job and both event handlers. A business module (for example an `*.Application` project that defines
  > and publishes notifications) changes its csproj reference from `Dignite.Abp.Notifications` to
  > `Dignite.Abp.Notifications.Abstractions` and its `[DependsOn(typeof(AbpNotificationsModule))]` to
  > `[DependsOn(typeof(AbpNotificationsAbstractionsModule))]`. Left as it is, the module still compiles and pulls the
  > implementation into every process that hosts it — in a publisher with `.Client` the local publisher then wins and
  > the publisher distributes its notifications itself instead of sending them to the notification service.

  **Migrate — hosts:**
  - Monolith (distributes in-process): replace `Dignite.Abp.Notifications.Distribution` with `Dignite.Abp.Notifications`
    and `AbpNotificationsDistributionModule` with `AbpNotificationsModule`. A host with `Dignite.NotificationCenter.Application`
    gets it from there.
  - Publisher of a split deployment: replace `Dignite.Abp.Notifications.Remote` with `Dignite.Abp.Notifications.Client`
    (`AbpNotificationsClientModule`, namespace `Dignite.Abp.Notifications.Client`), and
    `Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore` with `Dignite.Abp.Notifications.EntityFrameworkCore`.
  - Notification service: `Dignite.Abp.Notifications.Distribution` → `Dignite.Abp.Notifications`,
    `Dignite.Abp.Notifications.DefinitionStore(.EntityFrameworkCore)` → `Dignite.Abp.Notifications.Domain` /
    `Dignite.Abp.Notifications.EntityFrameworkCore` (`AbpNotificationsEntityFrameworkCoreModule`).
  - Namespaces: `Dignite.Abp.Notifications.DefinitionStore` → `Dignite.Abp.Notifications` (the record entities,
    `NotificationDefinitionStoreOptions`, `NotificationDefinitionStoreDbProperties`, the saver, the dynamic store),
    `Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore` → `Dignite.Abp.Notifications.EntityFrameworkCore`.
    `NotificationDefinitionStoreDbContext`, `ConfigureNotificationDefinitionStore()`, the `NotificationCenter`
    connection string name and the tables are unchanged. The record entities' CLR names change, so a host's EF model
    snapshot names them differently; the demo host's snapshot was updated and `dotnet ef migrations
    has-pending-model-changes` reports no change, so no migration is needed for the rename itself.

- **Breaking - `.Client` and the in-process implementation no longer exclude each other; the local publisher wins.**
  rc.23's Remote module failed the start when an `INotificationDistributor` was registered. `RemoteNotificationPublisher`
  now registers with `TryRegister` (it used `ReplaceServices`), as ABP's `HttpClientUserRoleFinder` yields to a local
  `UserRoleFinder`: with both packages installed, `DefaultNotificationPublisher` is resolved in either module order and
  the process distributes itself; with only `.Client`, `RemoteNotificationPublisher` is.
- **Breaking - a host without any publisher resolves `NullNotificationPublisher`** instead of failing on the first
  resolution of `INotificationPublisher`: each publish logs a warning naming the notification and returns. It is
  registered by `AbpNotificationsAbstractionsModule.PostConfigureServices` (`TryAdd`, after every module's own
  registrations), so the local and the remote publisher always win over it.
- **Breaking - `StaticNotificationDefinitionSaver` takes an `IDistributedEventBus`**, and
  `UpdateChangedNotificationsAsync` takes the `List<string>` that receives the names of the inserted and changed
  definitions (ABP's `StaticPermissionSaver` signature). Only subclasses of the saver are affected.

### Removed

#### notifications

- The package ids `Dignite.Abp.Notifications.Distribution`, `Dignite.Abp.Notifications.Remote`,
  `Dignite.Abp.Notifications.DefinitionStore` and `Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore` (see
  the table above), the modules `AbpNotificationsDistributionModule`, `AbpNotificationsRemoteModule`,
  `AbpNotificationsDefinitionStoreModule`, `AbpNotificationsDefinitionStoreEntityFrameworkCoreModule`, and the startup
  check that made Remote and Distribution exclusive.

## [10.0.0-rc.23] - 2026-10-09

### Added

#### notifications

- **Split deployment: publish in one process, distribute in another.** The new `Dignite.Abp.Notifications.Remote`
  package replaces `INotificationPublisher` with `RemoteNotificationPublisher`, for a service whose inbox and channels
  live in a separate notification service. It checks that the definition exists, serializes the payload, resolves the
  channels with the local `INotificationChannelResolver` (the routing rules are configured where the business module
  runs) and publishes one `NotificationPublishRequestedEto` per notification — into the publisher's outbox when a unit
  of work is active, in the same transaction as the business change. The notification service handles it with
  `NotificationPublishRequestedHandler`: the publisher's notification id, tenant, payload JSON and channels are kept,
  small explicit fan-outs are distributed inline and the rest through the service's own job. Remote and Distribution
  in one process fail the start. See "Split deployment" in the notifications README.
- **`NotificationPublishRequestedEto`** (Abstractions, wire name `Dignite.Abp.Notifications.NotificationPublishRequested`):
  a flat, default-System.Text.Json POCO carrying the payload as `DataJson`, the recipients and the resolved channels.
  It implements `IMultiTenant`, so the receiving bus runs the handler in its tenant, host included. Abstractions now
  references `Volo.Abp.MultiTenancy.Abstractions`.
- `NotificationInfo.Channels` carries channels resolved by a publisher in another process; the distributor uses them
  as they are and asks neither the resolver nor the definition manager for routing.
- `NotificationStore.InsertNotificationAsync` skips a notification id it already holds, so a second run of the same
  distribution (a redelivered publish request, a retried job) does not fail on the primary key.
- **Definition catalog: `Dignite.Abp.Notifications.DefinitionStore` and
  `Dignite.Abp.Notifications.DefinitionStore.EntityFrameworkCore`.** A notification service that hosts no business
  module learns the publishers' definitions the way an ABP microservice learns other services' permissions: every
  process saves its static definitions to shared tables at startup (`StaticNotificationDefinitionSaver`, in the
  background with Polly retries), and a process with `NotificationDefinitionStoreOptions.IsDynamicNotificationStoreEnabled`
  reads them (`DynamicNotificationDefinitionStore`, reloaded within 30 seconds of a change through a common stamp in the
  distributed cache). Each part follows ABP's permission management domain, including the per-application hash that
  skips unchanged saves and the rule that a save deletes only what `DeletedNotifications` / `DeletedNotificationGroups`
  list, so services sharing the tables never delete each other's records. Display texts are stored by resource name
  (`L:Resource,Key`) and resolved in the reader, through ABP's external localization store when the resource is not
  registered there. The EF Core package maps `NotifDefinitionGroups` / `NotifDefinitions` on the `NotificationCenter`
  connection string (`[IgnoreMultiTenancy]`) and adds `ConfigureNotificationDefinitionStore()` for the host's migration
  DbContext; no migrations ship. The process that reads the store must also turn on ABP's
  `IsDynamicPermissionStoreEnabled` / `IsDynamicFeatureStoreEnabled` (not checked; see the README). A MongoDB
  implementation is a follow-up. The repository now pins `Polly` 8.6.3 (the version ABP 10.5's own stores use). See
  "Definition catalog" in the notifications README.
- `IStaticNotificationDefinitionStore` / `StaticNotificationDefinitionStore` (the providers' definitions, built once)
  and `IDynamicNotificationDefinitionStore` with its empty default `NullDynamicNotificationDefinitionStore` in Core,
  after ABP's static and dynamic feature definition stores.

### Changed

#### notifications

- **Breaking - the distribution pipeline moved out of `Dignite.Abp.Notifications` into the new
  `Dignite.Abp.Notifications.Distribution` package.** Every process that referenced Core registered the distribution
  job and the delivery event handler, so two services carrying business modules shared the job queue and both received
  every `NotificationDeliveryRequestedEto`. Core keeps what business modules use — definitions, routing, the
  `INotificationPublisher` contract, `NotificationInfo` and the other info records, the `INotificationStore` /
  `INotificationDistributor` / `INotificationPermissionChecker` contracts — with unchanged namespaces and package id, so
  modules compiled against rc.22 keep working. `DefaultNotificationPublisher`, `DefaultNotificationDistributor`,
  `NotificationDistributionJob(Args)`, `NotificationDeliveryRequestedHandler`, `NullNotificationStore`,
  `AlwaysGrantedNotificationPermissionChecker`, `NotificationSubscriptionManager` and `NotificationDistributionOptions`
  moved to Distribution (same `Dignite.Abp.Notifications` namespace), as did the unhosted-channel warning and the
  stateless no-channel startup check; Core keeps the check for rules naming undefined notifications.
  **`AbpNotificationsModule` no longer registers the delivery handler, the job or `NotificationDistributionOptions`**,
  and depends only on Abstractions and Features. **Migrate:** a host without the Notification Center adds
  `Dignite.Abp.Notifications.Distribution` and `[DependsOn(typeof(AbpNotificationsDistributionModule))]`; otherwise
  `INotificationPublisher` has no implementation. `Dignite.NotificationCenter.Application` depends on Distribution, so a
  host with the Notification Center gets it automatically. Test hosts that relied on Core's publisher add Distribution
  too.
- **Breaking - `NotificationInfo.Data` is now `NotificationInfo.DataJson`**, the discriminator-tagged JSON produced by
  `INotificationDataSerializer`. The publisher serializes once, at the publish boundary — an unregistered payload type
  now fails there, before anything is persisted or enqueued — and the string travels from there: the store writes and
  returns it unchanged, the distributor copies it onto every delivery event, and the job args round-trip through any
  serializer. A process that distributes no longer needs the payload's CLR type. `UserNotificationAppService`
  hydrates `UserNotificationDto.Data` through the tolerant `INotificationDataSerializer.Deserialize`, so the REST and C#
  client contracts are unchanged. `DefaultNotificationDistributor` and `NotificationStore` no longer take the
  serializer, and `NotificationStore.DeserializeDurableData` is gone. `DefaultNotificationPublisher` takes the new
  `NotificationDistributionDispatcher` (the inline-or-job decision it now shares with the remote-publish handler) and
  the serializer.
- **Breaking - the distribution job is named `Dignite.Abp.Notifications.Distribute`** (`[BackgroundJobName]`) instead of
  the args type's full name, so its queue is a stable contract of Distribution. Jobs still queued under the old name are
  not picked up after the upgrade; drain the queue first.
- `NullNotificationStore` and `AlwaysGrantedNotificationPermissionChecker` register with `TryRegister`: the packages
  that replace them depend on Core, not Distribution, so module order no longer guarantees that the replacement is
  registered last.
- **Breaking - `Dignite.Abp.Notifications.Identity` no longer depends on `Volo.Abp.Identity.Domain`; the host must be
  able to resolve ABP's `IUserRoleFinder`.** `IdentityNotificationPermissionChecker` used
  `IIdentityUserRepository` and `IUserClaimsPrincipalFactory<IdentityUser>`, which tied the package to the Identity
  database. It now asks `IUserRoleFinder` (`Volo.Abp.Identity.Domain.Shared`) for the recipient's role names, builds
  the principal ABP's permission providers read (`UserId`, one `Role` per role, and `TenantId` when a tenant is
  current) and calls `IPermissionChecker`. The package references `Volo.Abp.Authorization` and
  `Volo.Abp.Identity.Domain.Shared`; the package id, namespace, class name and `AbpNotificationsIdentityModule` are
  unchanged. **Migrate:** a monolith that already installs `Volo.Abp.Identity.Domain` needs nothing, it registers
  `UserRoleFinder`. A host without it must register an `IUserRoleFinder`, normally by installing an Identity
  `HttpApi.Client` package (`Volo.Abp.Identity.Pro.HttpApi.Client`, whose `HttpClientUserRoleFinder` calls the
  Identity service), and enable `IsDynamicPermissionStoreEnabled`. Installing the package no longer pulls in the
  Identity domain or needs the Identity database. A recipient that no longer exists is no longer rejected up front:
  it has no roles and is granted only what is granted to it directly.
- **Breaking - `INotificationDefinitionManager` is asynchronous**: `Get` / `GetOrNull` / `GetAll` / `GetGroups` /
  `GetGroupOrNull` become `GetAsync` / `GetOrNullAsync` / `GetAllAsync` / `GetGroupsAsync` / `GetGroupOrNullAsync`,
  because the manager now merges the definitions of this process with those of the definition catalog (static first,
  a dynamic definition or group only under a name no static one has, as ABP's `FeatureDefinitionManager`). Business
  modules' production code uses only the definition API and is unaffected; code that calls the manager — tests that
  assert a definition included — awaits the new methods. `NotificationDefinitionManager` is transient (it was a
  singleton) and no longer builds the definitions: a custom registry overrides
  `StaticNotificationDefinitionStore.CreateGroups` (or replaces `IStaticNotificationDefinitionStore`) instead of
  `NotificationDefinitionManager.CreateGroups`. The startup checks read the static definitions only.
  `UserNotificationAppService.MapToDto` and `TryResolveGroupFilter` become `MapToDtoAsync` and
  `ResolveGroupFilterAsync`, and `NotificationSubscriptionAppService.GetGroupOrNull` becomes `GetGroupOrNullAsync`.
- **Breaking - a remote publish request whose notification is unknown is refused, so the event inbox retries it.**
  `NotificationPublishRequestedHandler` looks the name up (its own definitions and the catalog) before writing anything
  and throws when it finds none: the definition carries the permission and feature requirements that apply at
  delivery, and until now such a request was distributed to no one and marked processed. The usual cause is a
  publisher whose definitions have not reached the catalog yet, which a retry outlives; configure the notification
  service with `AbpEventBusBoxesOptions.InboxProcessorFailurePolicy = RetryLater` (ABP's default `Retry` re-runs the
  event every period and holds back the events behind it). The handler's constructor takes an
  `INotificationDefinitionManager`.

## [10.0.0-rc.22] - 2026-10-09

### Added

#### notifications

- **Personal data erasure on `GdprUserDataDeletionRequestedEto`.** The Notification Center now depends on
  `Volo.Abp.Gdpr.Abstractions` and handles ABP's GDPR deletion event (`GdprUserDataDeletionRequestedHandler`): it
  deletes the user's inbox rows (read and unread), notification subscriptions and push devices, in every tenant.
  Rows are removed in bulk by user id alone, because before ABP 10.7 the event carries no tenant. The shared
  `Notification` payload is left to the host's retention job. Nothing in this repository publishes the event - the
  host needs a publisher; without one the handler never runs. See "Personal data erasure (GDPR)" in the notifications
  README.
- **Breaking - `INotificationStore.DeleteAllUserDataAsync(userId)`** erases the user's inbox rows and subscriptions in
  every tenant; custom `INotificationStore` implementations must add it (`NullNotificationStore` is a no-op).
  `PushDeviceManager.RemoveAllAsync(userId)` does the same for push devices. `NotificationStore` takes `IDataFilter` as
  a new constructor argument.

## [10.0.0-rc.21] - 2026-10-07

### Changed

#### notifications

- **Breaking - delivery channels are routed by the host, not fixed in the definition.**
  `NotificationDefinition.UseChannels(...)` / `GetChannelsOrNull()` and the `NotificationChannels` helper are
  gone. Routing lives in the new `NotificationRoutingOptions` (Core), which has two levels only: a rule per
  notification name (`ForNotification(name, channels...)`, `ForNotifications(names, channels...)`,
  `InboxOnly(names...)`) and a `Default` for notifications without a rule. A module adds defaults for its own
  notifications in `ConfigureServices`; the host module, configured last, overrides any of them - later writes win
  and a repeated rule for the same notification replaces the earlier one as a whole. Groups carry no routing.
  Core and Abstractions know no channel names (each notifier package keeps its own `XxxNotifier.ChannelName`).
  `Default` is never "every installed channel", so installing a notifier package cannot fan existing
  notifications out to a new channel; `InboxOnly` is the explicit rule that overrides `Default`.
  **Migrate** `.UseChannels("SignalR", "Email")` to
  `Configure<NotificationRoutingOptions>(o => o.ForNotification(name, "SignalR", "Email"))`.
- **`INotificationChannelResolver`** (Core) decides the channels for one notification; the default reads
  `NotificationRoutingOptions`. It is called once per notification, before recipients are batched, inside the
  notification's tenant scope, so a host can replace it for tenant- or severity-dependent routing. Per-user channel
  preferences are not part of it. `DefaultNotificationDistributor` takes it as a new constructor argument.
- **Startup validation of routing.** A rule for a notification no provider defines fails the start (catches typos); a
  channel no notifier in the process hosts logs a warning naming the channel and the notifications that use it
  (`NotificationRoutingOptions.RequireHostedChannels` makes it fail, for hosts that deliver every channel
  themselves); in stateless mode (`NullNotificationStore`) a notification that resolves to no channel fails at
  startup instead of at publish time.
- **Breaking - `INotificationsClient.ReceiveNotification` takes `SignalRNotificationMessage`**, not
  `NotificationPayload`. `NotificationPayload` stays as the in-process typed view the Email and Push notifiers
  render from, but it is no longer a wire contract. Clients that read the message by property name see the same
  fields; only `data` changes shape (see Fixed). The MessagePack hub protocol is not supported.

### Fixed

#### notifications

- **The SignalR push carried `"data": {}`.** The hub protocol serializes with its own System.Text.Json options,
  which do not know the polymorphic `NotificationData` converter, so clients received neither the `type`
  discriminator nor the content. The bundled MVC and Angular bells only use the push as a refresh prompt and were
  unaffected; in stateless forwarding mode, where the push is the only carrier of the payload, custom clients
  received nothing usable. `data` is now the raw discriminator-tagged JSON object
  (`{"type":"Dignite.Message","message":"..."}`), the same shape the REST inbox returns for
  `UserNotificationDto.Data`. A module-wide rule now covers this class of bug: a wire contract serialized by an
  engine the module does not configure never carries a live `NotificationData`.

## [10.0.0-rc.20] - 2026-10-07

### Added

#### notifications

- **Notification groups.** Every definition now belongs to a group, as ABP permissions do:
  `context.AddGroup(name, displayName).AddNotification(name, displayName)`. Duplicate group names, and duplicate
  definition names across groups, fail at startup. The inbox API gains a `GroupName` filter,
  `GET /api/notification-center/notifications/groups` (groups in definition order with their unread counts), and
  `GroupName` / `GroupDisplayName` on `UserNotificationDto` and `NotificationSubscriptionDto`; notifications whose
  definition no longer exists fall into a synthetic "Other" group. The subscription settings UI (MVC and Angular)
  lists definitions under group headings.
- **Inbox page** for MVC (`/NotificationCenter/Notifications`) and Angular (`NotificationInboxComponent`, served by
  `createRoutes()` and replaceable via `eNotificationCenterComponents.Notifications`): group tabs with unread
  counts, an all/unread filter, paging, per-item delete, and "mark all as read" / "clear read". It is reached from
  the bell, not the main menu.
- **The bell lists the ten most recent notifications, read and unread**, highlighting unread ones (the badge still
  counts only unread), with a "View all" link to the inbox page. Reading a notification no longer removes it from
  the bell.
- **Device push channel.** `Dignite.Abp.Notifications.Push` adds the `"Push"` channel: a definition says
  `UseChannels("Push")` and the notification reaches the recipient's phones. Which delivery service carries
  a message is decided per device - each registered device names the `IPushProvider` that issued its
  token - so one channel spans Expo, FCM and APNs. Devices come from a new `IPushDeviceStore` seam (a null
  store until something replaces it), content from an `INotificationPushContentProvider` chain built once
  per device culture, and every message carries the inbox keys (`notificationId`, `notificationName`,
  `entityTypeName`, `entityId`) as silent data. A device the provider reports dead is removed. Delivery
  stays best-effort: no retry, no delivery state. Core, the distributor and
  `NotificationDeliveryRequestedEto` are unchanged.
- **Expo Push Service provider.** `Dignite.Abp.Notifications.Push.Expo` sends through Expo's push API (one
  HTTP endpoint for iOS and Android, batches of 100), with `ExpoPushOptions.AccessToken` for Expo's
  enhanced push security. Only push tickets are read; receipts are not polled.
- **Push device registry in the Notification Center.** A new `PushDevice` aggregate (EF Core and MongoDB)
  records the phones each user can be pushed to, with `POST /api/notification-center/push-devices/register`
  and `/unregister` for the app (token in the body, never the URL). A registration takes the request culture
  as the device's language and the caller's `session_id` claim as its login session; a token registered to
  someone else - in any tenant - moves to the caller; a user keeps at most `PushDeviceOptions.MaxDevicesPerUser`
  devices (default 10), the least recently seen dropping out. `Dignite.NotificationCenter.Push` serves the
  push channel's devices from it.
- **Push follows the login session (optional).** `Dignite.NotificationCenter.Push.Identity` forgets a device
  whose ABP Identity session no longer exists instead of pushing to it - signed out, revoked, or cleaned up
  as inactive. Meaningful on hosts with Identity Pro session management; inert elsewhere.

### Changed

#### notifications

- **Breaking - definitions must be grouped.** Definition providers use `AddGroup(...).AddNotification(...)`; the
  top-level `context.Add` and the public `NotificationDefinition` constructor are gone (custom definition managers
  override `CreateGroups()` instead of `CreateDefinitions()`). `INotificationStore` gains include/exclude name
  filters on the inbox list/count methods and `GetUnreadCountsByNotificationNameAsync`, and implementations must
  populate `UserNotificationInfo.NotificationName`.
- **Schema change:** `UserNotification` carries a copy of `NotificationName` (new column plus a
  `(TenantId, UserId, NotificationName, State, CreationTime)` index on both providers), so group filtering and
  per-group unread counts stay single-table queries. EF Core hosts need a migration.
- `NotificationCultureResolver` (Abstractions) now holds the recipient-culture fallback that
  `EmailNotifier.ResolveCulture` used to implement privately, so the email and push notifiers share it;
  `EmailNotifier` switches cultures with ABP's `CultureHelper.Use`. Behaviour is unchanged.
- `LocalizableMessageNotificationData.Localize(IStringLocalizerFactory)` (Abstractions) is now the one
  rendering rule for localizable messages; the email and push content providers and the MVC inbox view
  component call it instead of each carrying a copy. Output is unchanged.
- **Core's delivery handler builds only the notifier for the delivery's channel.** It used to construct every
  channel's notifier, with its whole dependency graph, for every delivery event and keep one - so an email
  delivery also built the push and SignalR notifiers, and any one notifier failing to construct broke every
  channel. Each channel module now maps its channel to its notifier type in the new `NotificationNotifierOptions`
  (Abstractions), and the handler resolves just that type. A channel claimed by two types fails the application
  start instead of the first delivery. **Custom notifiers must register:**
  `Configure<NotificationNotifierOptions>(o => o.Notifiers.Add<TNotifier>(channelName))` - one exposed only as
  `INotificationNotifier` is no longer called. `INotificationNotifier` itself is unchanged.
- **A notifier that cannot be resolved no longer escapes the delivery handler.** A notifier whose construction
  fails, or that answers to a different name than the channel it is registered for, is now logged at Error (with
  the exception - building a notifier involves no recipient data) and the delivery dropped, as best-effort as a
  failed delivery, instead of throwing back into the event bus.
- **Schema change: every EF Core host needs a migration** for the new `{prefix}PushDevices` table (hosts own
  their migrations; this module ships none). Hosts that implement `INotificationCenterDbContext` or
  `INotificationCenterMongoDbContext` themselves must also add the new `PushDevices` property.

### Fixed

#### notifications

- **MVC bell mark-as-read and the subscription toggles did nothing.** `notification-center.js` called
  `dignite.abp.notificationCenter.*`, but ABP generates the proxies under `dignite.notificationCenter.*`.
- **MVC bell badge double-counted and missed pushes.** Each SignalR push added one to the server-rendered count,
  counting a notification twice when it arrived after the page rendered, and pushes missed while disconnected were
  never counted. The bell now re-reads `GET .../notifications/unread-count` after every push and on reconnect, as
  the Angular bell already did.

## [10.0.0-rc.19] - 2026-10-04

### Fixed

#### aspnetcore-mcp

- **A tool call's changes are now saved before its result is returned.** The only unit of work an MCP
  request had was the one `AbpUnitOfWorkMiddleware` completes after the whole pipeline - by which time
  the SDK had already written the tool's result to the response. A save that then failed (a unique index,
  a concurrency conflict) could not become an error result: the middleware threw into a started
  response and the client's connection was cut off. `AbpMcpUnitOfWorkFilter` now gives each tool call,
  resource read and prompt the unit-of-work handling `AbpUowActionFilter` gives an MVC action: it takes
  over the reserved unit of work, saves before returning, and rolls back on failure, so a failed save
  comes back as the call's structured error. Found by an end-to-end run against the Site host, where
  re-uploading a file whose earlier copy had been deleted hit `FileExplorer`'s MD5 unique index.
- **Failures reported as tool results are logged.** `McpToolErrorFilter` turns every exception into an
  error result, so nothing further up ever saw it: an unexpected failure reached the client as "an
  internal error occurred" and left no trace on the server. It now logs and notifies exactly as
  `AbpExceptionHandlingMiddleware` does for the HTTP API - level from the exception (business and
  validation errors as warnings), `ExcludeExceptionFromLoggerSelectors` honoured, `IExceptionNotifier`
  called.

## [10.0.0-rc.18] - 2026-10-04

### Added

#### aspnetcore-mcp

- **New top-level tree `aspnetcore-mcp/`, package `Dignite.Abp.AspNetCore.Mcp`.** Hosts the one
  Model Context Protocol server an ABP application can have and lets any number of modules contribute
  tools, resources and prompts to it. The C# MCP SDK keeps one server per service container, so
  everything that exists once per server - transport, the `/mcp` endpoint, ABP-permission filtering,
  one structured error envelope, server info and instructions - now lives here instead of in whichever
  feature module happened to configure it first. Each contributor claims a namespace with
  `AddAbpMcpModule(name, …)` (tool/prompt prefix `{name}_`, resource URI scheme), and the server
  refuses to start on any overlap: the SDK otherwise keeps the first of two same-named tools and drops
  the other silently. Tool classes are resolved from ABP's container on every call, so
  `[Dependency(ReplaceServices = true)]` works for them, which the SDK's own `WithTools<T>()` does not
  allow. The endpoint authorizes against the default policy and `AddAbpMcpAuthenticationDiscovery`
  takes over only the 401 challenge (RFC 9728), so ABP's dynamic claims are not discarded on MCP
  requests. Not a fourth module: it carries no domain model, and depending on it is not the
  cross-module reference the "three modules never reference each other" invariant guards against.
  Modules with dynamic resources contribute to `resources/list` through `IAbpMcpResourceListContributor`
  instead of the SDK's single `WithListResourcesHandler` slot. The endpoint carries a request-body limit
  (`AbpMcpServerOptions.MaxRequestBodySize`, 4 MB by default) that routing applies before the body is read,
  since a tool can only check its arguments once the whole request is already buffered.
  `AddAbpMcpAuthenticationDiscovery` wraps the host's `IAuthorizationMiddlewareResultHandler` with its
  lifetime intact and fails startup if a later registration replaces the wrapper; a second call only
  replaces the metadata, and the MCP scheme no longer forwards authentication to a `Bearer` scheme the host
  may not have (the SDK's default). Primitives registered outside any module can be admitted with
  `AbpMcpServerOptions.AllowUnownedPrimitives`, for third-party tool libraries.

#### file-storing

- **New package `Dignite.FileExplorer.Mcp`**: `file_explorer_*` MCP tools over the file explorer -
  list containers, list/create directories, list/get/upload/update/delete files - each calling the
  existing application services. Only containers the host lists in `FileExplorerMcpOptions.Containers`
  are reachable. Uploads are base64 and capped by `FileExplorerMcpOptions.MaxUploadSize` (5 MB); the module
  raises the MCP endpoint's request-body limit just enough to fit that, so a larger request is refused before
  it is read. A file looked up by id answers "not found" alike when it is missing, unreadable to the caller,
  or in a container not exposed to MCP, so probing ids reveals nothing. A listed container that cannot be
  read is left out of `file_explorer_list_containers` (and logged) instead of failing it for the others.

### Fixed

#### file-storing

- **`FileDescriptorAppService.GetListAsync` listed every user's files to a caller with no user id.** Without
  `Files.Management` it narrows the list to `CreatorId = CurrentUser.Id` - but for a principal authenticated
  without a user (a client-credentials token) that is `null`, and the repository reads a `null` creator as
  "no owner filter", returning all files in the container. Such a caller owns no files, so it now gets an
  empty list. Reachable over the REST API and, now, through `file_explorer_list_files`.
- **`DirectoryDescriptorAppService.GetListAsync`/`CreateAsync` threw a null dereference for a caller with no
  user id** (`CurrentUser.Id.Value`). Directories are per user, so such a caller is now refused with an
  authorization error carrying the new, localized code `Dignite.FileExplorer:Directory:0008`.
- **`FileDescriptorAppService.CreateAsync` accepted any `DirectoryId`.** Moving a file (`UpdateAsync`) already
  required the directory to exist and share the file's container, owner and tenant; creating one checked
  nothing, so a new file could be filed under another user's or another container's directory, or a directory
  that does not exist. Creation now applies the same check, before anything is written to blob storage, and
  answers `Dignite.FileExplorer:Directory:0002` (directory does not exist) like a move does.

### Changed

#### file-storing

- The file route prefix moved from a private constant on `FileDescriptorController` to
  `FileExplorerRemoteServiceConsts.FilesRoutePrefix`, so the HTTP API and the MCP tools build file URLs
  from one definition. The route itself is unchanged.

## [10.0.0-rc.17] - 2026-09-23

### Fixed

- **A tagged release could fail *after* publishing everything, purely because npmjs had not finished
  serving what it had just accepted.** `v10.0.0-rc.16` pushed all the NuGet packages and all five
  Angular packages successfully, then failed at "Verify published packages install as a single copy
  each" with `Couldn't find any versions for "@dignite/ng.flex-fields" that matches "^10.0.0-rc.16"` -
  and because "Create draft GitHub Release" is a later step in the same job, the release was left
  half-cut: every package public, no GitHub Release, no release notes, no attached artifacts. npmjs
  records a publish asynchronously: `npm publish` returns as soon as the tarball is accepted, printing
  "Your package is being processed and may take a few minutes to become available", while the version
  enters the packument a resolver reads some time later. For `@dignite/ng.flex-fields` that gap was
  126 seconds - `npm publish` printed `+ @dignite/ng.flex-fields@10.0.0-rc.16` at 00:23:04Z, npmjs's
  own timestamp for the version is 00:25:10Z - and `verify-npm-single-copy.mjs`'s `published` mode
  gave up at 00:24:59Z, twelve seconds early: five `yarn install` attempts spaced 10/20/30/40s apart,
  roughly 100 seconds of budget against a window npm itself describes in minutes. Enlarging that
  budget would not have been the fix, because retrying `yarn install` is the wrong instrument for
  this: yarn aborts on the first name it cannot resolve, so its exit code cannot distinguish "one of
  five is still propagating" from "the published set is broken", and each attempt spends a full
  resolution pass to learn nothing. `published` mode now polls the registry's own abbreviated
  packument for each of the five packages until every one serves the expected version - 15 minutes by
  default, `DIGNITE_NPM_PROPAGATION_TIMEOUT_SECONDS` to override - logging each package as it appears,
  and installs only then. Past that point a `yarn install` failure is about resolution, which is the
  only thing this check is qualified to judge; the three retries left are for a genuine transient such
  as a CDN edge lagging the packument just read, and a real duplicate fails identically on every
  attempt, so it cannot be retried away. A propagation timeout now says so in those words, to stop a
  future reader from re-diagnosing it as the duplicate of issue #211.

#### flex-fields

- **`@dignite/ng.flex-fields-ckeditor` and `@dignite/ng.flex-fields-file-explorer` now depend on their
  sibling packages at the exact release version instead of a `^` range.** Both declared
  `@dignite/ng.flex-fields` (and the file-explorer bolt-on also `@dignite/ng.file-explorer`) as
  `^10.0.0-rc.16`. Every `@dignite/ng.*` package ships from this repository at one lockstep version,
  from the same commit, so a range could never admit a second compatible version — only an *older*
  published sibling, which is exactly what Yarn Classic picks whenever npm's `latest` tag trails the
  newest version. That leaves two copies of `@dignite/ng.flex-fields` in a host, and because Angular DI
  keys off object identity, the module-scoped `FLEX_FIELD_TYPES` token becomes two distinct keys:
  `provideCKEditorFieldType()` registers into one while `FieldTypeResolver` reads the other, so every
  field type looks unregistered at runtime with nothing failing at install or build time. That is the
  actual root cause of issue #211, which the single-copy checks in `release.yml` had only been
  detecting. A host must now install every `@dignite/ng.*` package at the same version — which it
  already had to for them to work together. `verify-version-lockstep.ps1` requires the exact version
  for any `@dignite/*` dependency, so CI rejects a range reintroduced here.
- **`DateTimeViewComponent`, the read-only view for `DateTime` fields, ignored the field's
  `DateTime.InputMode` configuration and always rendered `value | shortDateTime`.** A field configured
  for `InputMode = Date` or `InputMode = Month` therefore showed a spurious time part in every
  read-only context — a bare field, or a `Table` column rendered through `ff-table-view`, which
  dispatches to this same component via `ff-flex-field-view`. The edit-mode counterpart,
  `DateTimeControlComponent`, already read `configuration['DateTime.InputMode']`, looked up the
  matching Angular `DatePipe` format string in `DATE_INPUT_MODE_FORMATS`, and formatted with it — the
  view component never did the same lookup despite receiving the same `field.configuration` on its
  `fields` input. `DateTimeViewComponent` now performs that lookup itself, injecting `DatePipe` the
  same way, and falls back to the `shortDateTime` pipe only when no `fields` input is bound or the
  configured mode isn't in the table, so existing usages that never pass `field.configuration` keep
  rendering exactly as before.

### Added

#### flex-fields

- **The `Select` field types now load their `ng-zorro-antd` stylesheet themselves, by bundle name,
  exactly the way `abp-tree` loads its own.** ng-zorro-antd ships no component styles, so `<nz-select>`
  had been rendering against whatever antd CSS a host happened to have declared. `SelectControlComponent`
  and `SelectSearchComponent` now ask for `ng-zorro-antd-select.css` once per application at init; a
  host serves it with a single `angular.json` `styles` entry —
  `node_modules/ng-zorro-antd/select/style/index.min.css`, `inject: false`,
  `bundleName: "ng-zorro-antd-select"` — and can switch the loading off with the new
  `DISABLE_FLEX_FIELDS_STYLE_LOADING_TOKEN` when it bundles that CSS another way. A host that has not
  declared the entry now gets one console error naming the missing file and quoting the entry to add,
  rather than a silently unstyled control. Shipping an aggregated stylesheet inside the package was
  considered and rejected on two counts: ng-zorro-antd declares `less` before `style` in its
  `./<component>/style/*` export, so a bare `@import 'ng-zorro-antd/select/style/index.min.css'`
  resolves to a non-existent `index.min.css.less` under the Angular CLI's stylesheet bundler, and
  freezing a copy of a peer dependency's CSS into this package's release cycle is not an acceptable
  substitute. The demo's dead `ng-zorro-antd-tree-select` entry went with it — nothing renders
  `nz-tree-select`. See the package README's new "Styles" section for the host-side contract. (#232)
- **`@dignite/ng.flex-fields-ckeditor` loads CKEditor 5's stylesheet the same way, through the same
  loader.** `FlexFieldsStyleLoader` is shared across the package family; each package declares its own
  bundle constant. The bolt-on used to `@import 'ckeditor5/ckeditor5.css'` from its control
  component's stylesheet, which ng-packagr inlined at build time: 241 KB of third-party CSS compiled
  into the published `fesm2022` bundle (471 KB, 522 `.ck-editor` rules) and, because a host registers
  the field type in its application config, shipped in that host's *initial* bundle whether or not a
  rich-text field was ever opened — while pinning the CSS to whatever `ckeditor5` version the package
  was built against, though the editor's own JavaScript comes from the host's installed copy via
  `await import('ckeditor5')`. `CKEditorControlComponent` now asks for the host's `ckeditor5` bundle at
  init: one `angular.json` `styles` entry — `node_modules/ckeditor5/dist/ckeditor5.css`,
  `inject: false`, `bundleName: "ckeditor5"`, exported as `CKEDITOR5_STYLE`. The bolt-on's bundle drops
  to 36 KB and this repo's demo from a 2.20 MB initial bundle to 1.98 MB (483 kB to 455 kB
  transferred), back under the 2 MB budget the build had been warning about. A host that has not
  declared the entry gets one console error naming the file and quoting the entry, instead of an editor
  that silently renders as blank/collapsed space. `DISABLE_FLEX_FIELDS_STYLE_LOADING_TOKEN` is
  family-wide: `true` silences every bundle loaded through this service, in every
  `@dignite/ng.flex-fields*` package. (#232)

### Security

#### notifications

- **`Dignite.Abp.Notifications.Emailing` now requires Scriban `>= 7.2.7`, lifting the vulnerable 7.2.1
  that ABP 10.5.0 resolves.** The package reaches Scriban through `Volo.Abp.Emailing` →
  `Volo.Abp.TextTemplating.Scriban` 10.5.0, which resolves Scriban 7.2.1 — affected by the
  high-severity advisory GHSA-7jvp-hj45-2f2m. The root `Directory.Packages.props` now pins Scriban
  7.2.7, and central transitive pinning promotes it into this package's own published dependency list,
  so consumers restoring `Dignite.Abp.Notifications.Emailing` resolve the patched version too, not just
  this repository's builds. The pin goes once ABP rolls forward past the advisory on its own.

## [10.0.0-rc.16] - 2026-09-05

### Fixed

- **The single "install with Yarn Classic and check for a duplicate" verification
  `build/verify-npm-single-copy.mjs` performs for issue #211 ran only after both Angular publish
  steps in `release.yml`**, so a real duplicate reaching consumers would be reported only once the
  damage was already done - the same failure shape `v10.0.0-rc.14`'s own post-publish crash exposed:
  the check runs at all, but by the time it can fail, everything is already live on npmjs and GitHub
  Packages, and a failure there can only skip the draft GitHub Release, not stop a bad publish.
  `verify-npm-single-copy.mjs` now supports two modes. `packed` installs the five tarballs the
  workflow's own `npm pack` steps already produce, pointing each `@dignite/*` dependency at its
  tarball with a `file:` path instead of a registry range - Yarn Classic resolves a `file:`
  dependency's version from the tarball's own `package.json` and reconciles it against every other
  edge in the graph exactly as it would a registry-resolved copy, so it exercises the same
  duplicate-vs-dedupe logic without anything published yet. A new "Verify packed Angular packages
  install as a single copy each" step runs this immediately after the last pack step, with no `if:`
  guard, so both a `workflow_dispatch` preview build and a tagged release get the real gate.
  `published` is the previous behavior, kept as a lighter step after the npm publish - it still
  catches what a local tarball cannot, such as a dist-tag pointing at the wrong version, but a
  failure there is no longer the only line of defense.

- **The new `packed` mode above failed outright the first time it ran against a version that had
  never been published anywhere** (`Couldn't find any versions for "@dignite/ng.file-explorer" that
  matches "^10.0.0-rc.16"`), because it only pointed the *top-level* dependency at each tarball's
  `file:` path - a sibling package's *own* packed `package.json` (e.g.
  `flex-fields-file-explorer` depending on `flex-fields`) still declares that edge as a plain semver
  range, and Yarn Classic resolves a `file:` request and a semver-range request for the same package
  name as two independent lookups rather than reusing one to satisfy the other. It went to the npm
  registry for the range and failed, since nothing at that version exists there yet - exactly the
  case this check exists to run before. `verify-npm-single-copy.mjs`'s `packed` mode now also sets
  `resolutions` to the same five `file:` paths, forcing every occurrence of a name in the tree onto
  the local tarball regardless of what range asked for it. This doesn't weaken the check:
  `verify-version-lockstep.ps1` already rejects a drifted internal range before this step ever runs,
  so every internal `@dignite/*` range is already `^<the current version>` by the time `packed` mode
  installs.

- **This repository's own `flex-fields/angular` and `file-storing/angular` demo apps were themselves
  carrying the exact `ng-zorro-antd` duplicate `6f039ef` documented as an accepted cost of
  `@abp/ng.components` pinning that package at `~21.0.0-next.1` (i.e. `<21.1.0`): `21.3.3` at each
  workspace root, declared there only for the demo's own use, and `21.0.2` nested under
  `node_modules/@abp/ng.components/node_modules` - two module-scoped `NZ_CONFIG`/`NzConfigService`
  tokens, so a root-level `provideNzConfig()`/`provideNzI18n()` never reached the copy
  `@abp/ng.components`'s own controls (e.g. `abp-tree`) resolve.** That duplication is inherent to
  `@dignite/ng.flex-fields`'s and `@dignite/ng.file-explorer`'s published `^21.0.0` peer range - a
  real downstream host may need `21.3.x` for reasons the packages can't rule out, which is why that
  peer range is untouched - but nothing required these two *demo* apps to actually be such a host:
  they exist to exercise the published packages, not to prove a wide peer range works. `ng-zorro-antd`
  is now narrowed to `~21.0.2` in both `flex-fields/angular/package.json` and
  `file-storing/angular/package.json` - inside `@abp/ng.components`'s ceiling, and the same version
  that was already nested - which collapses both workspaces back to a single copy. Both demo apps'
  production builds (`yarn build:prod`) pass unchanged at the older version. A new
  `build/check-angular-package-duplicates.mjs`, wired into `ci.yml` immediately after each of the
  three Angular workspaces' `yarn install --frozen-lockfile` steps, generalizes
  `verify-npm-single-copy.mjs`'s duplicate check from the five `@dignite/*` packages it covers to any
  bare-or-scoped target list - `ng-zorro-antd` and `@angular/cdk` for now, the latter reaching these
  same packages by the same `@abp/ng.components` route even though no version conflict currently
  splits it. `notifications/angular` is checked too even though it declares neither package itself:
  it depends on `@abp/ng.components`, which pulls both in transitively, so it is exposed to the same
  failure mode the moment something else in that workspace narrows either range. Like its sibling
  script, a target that matches nothing installed fails the run rather than passing vacuously - a
  typo'd target or a check pointed at the wrong `node_modules` is otherwise indistinguishable from a
  clean tree.

### Added

#### flex-fields

- **Two new built-in field types, `Matrix` and `Table` — the first *composite* ones, whose
  configuration declares whole field definitions inline.** `Matrix` is a repeatable list of
  polymorphic blocks (the admin declares named block types up front, each with its own sub-fields);
  `Table` is a homogeneous grid over one shared column schema. Both were written and proven in
  Dignite.Site's `Dignite.FlexFields.Site` and are ported here **with the wire format unchanged** —
  registration keys `Matrix`/`Table`, configuration keys `Matrix.BlockTypes`/`Table.Columns`, and
  camelCase `{blockTypeName, values}` / `{values}` value arrays — so fields already stored against
  the Site implementation keep working as-is. They ship as built-ins rather than a bolt-on package
  because, unlike `FileExplorer` or `CKEditor`, they depend on nothing outside the kernel's own
  vocabulary; what made them worth moving is that the two contracts below have to be answerable
  without knowing either concrete type.
  - `Dignite.Abp.FlexFields.Abstractions` gains `MatrixFieldType`/`TableFieldType` and their
    configuration types, plus four kernel contracts they share: **`ICompositeFieldType`**
    (`GetInlineFields`, so a host can ask "does this type contain other fields, and which" without
    naming a concrete type — an interface rather than an `IsComposite` bool, because every caller
    that asks also has to walk those fields), **`INormalizesValue`** (`Normalize`, the canonical wire
    shape — deliberately *not* folded into `Validate`, which returns only errors and never the parsed
    value, so a value with the wrong key casing would otherwise validate cleanly and then be stored
    verbatim and be unreadable to every camelCase reader downstream), **`InlineFieldDefinition`**
    (one inline field; carries `Required`, which a `FlexFieldData` cannot), and
    **`CompositeFieldNesting`** (`MaxDepth = 3` and the bounded measurement that enforces it — a
    configuration is a tree of unbounded depth and every reader of it recurses, so it is capped once
    on write instead of guarded in each reader).
  - `Dignite.Abp.FlexFields.Web` gains `Views/Shared/FlexFields/Matrix.cshtml` and `Table.cshtml`,
    which recurse through the existing `<flex-field-view>` dispatch for each sub-field rather than
    re-implementing rendering per type. No `Search/` partials: both types have
    `IndexValueType == null` — a list of composite objects has no typed index column to decompose
    into — so neither can be marked `Searchable`.
  - `@dignite/ng.flex-fields` gains the matching config / control / view components
    (`ff-matrix-config|control|view`, `ff-table-config|control|view`), registered in
    `BUILT_IN_FIELD_TYPES`, so an existing `provideFlexFields()` call already covers them.
    `FieldTypeDefinition` gains an optional **`composite`** flag, which Matrix and Table set and the
    config editors use to stop offering composite types once the nesting limit is reached; the
    server's `CompositeFieldNesting` remains the authority, that mirror is a courtesy.
  - Neither contract is invoked by the kernel — a host calls them, and the demo now shows both:
    `ProductAppService` normalizes the value bag before validating and saving, and
    `ProductFieldAppService` refuses a too-deeply-nested configuration on create and update.
  - **Localization moved with them**: the `FieldType:Matrix`/`FieldType:Table`, `Matrix:*`, `Table:*`
    and `Validate:Matrix:*`/`Validate:Table:*` texts now live in the `FlexFields` resource
    (`Dignite.Abp.FlexFields.Abstractions`) instead of Site's own `FlexFieldsSite` resource, in all
    four shipped cultures (`en`, `ja`, `zh-Hans`, `zh-Hant`). Three general validation keys the
    Angular side's shared error-message helper needs came along with them: **`Validate:MinValue`**,
    **`Validate:MaxValue`** and **`Validate:MaxLength`**.

## [10.0.0-rc.15] - 2026-09-05

### Fixed

- **The post-publish "Verify published packages install as a single copy each" step crashed on the
  same `${NODE_AUTH_TOKEN}` placeholder that had broken every yarn command earlier in the job**, so
  the `v10.0.0-rc.14` run failed *after* it had already published all 33 NuGet packages and all five
  Angular packages: the draft GitHub Release was never created, and the single-copy check the step
  exists for never actually ran. `v10.0.0-rc.13`'s fix moved `actions/setup-node`'s `registry-url`
  onto a second `setup-node` call placed past the last yarn command in the job - but this step runs
  *after* publishing, and therefore after that second call, whose generated `.npmrc` stays exported
  as `$NPM_CONFIG_USERCONFIG` for every remaining step. Yarn Classic expands every env-var
  placeholder in its resolved config on every invocation and throws when one is unset, and nothing
  sets `NODE_AUTH_TOKEN` (npm Trusted Publishing does not use it). The step now runs with an empty
  `NPM_CONFIG_USERCONFIG` of its own, which is all it needs: it installs published, public packages
  from npmjs and authenticates nothing. Verified by hand against the published `10.0.0-rc.14` set -
  Yarn Classic resolves exactly one copy of each of the five packages.

- **`@dignite/ng.flex-fields` and `@dignite/ng.file-explorer` imported `@abp/ng.components/tree` while
  declaring `@abp/ng.components` a peer dependency, so on any install that does not resolve peers the
  package was simply absent and the published bundles carried an import that resolved to nothing.** It
  surfaced downstream as `Could not resolve "@abp/ng.components/tree"` against both packages' `fesm2022`
  output while bundling `@dignite/ng.site` - at which point both were already published. A peer
  dependency is a claim that *the consumer already has this*, and `@abp/ng.components` is not something
  an ABP Angular host is guaranteed to have: neither `@abp/ng.core` nor `@abp/ng.theme.shared` depends
  on it, only feature packages such as `@abp/ng.identity` and `@abp/ng.setting-management` do. So a host
  that used neither had no reason to have it, and `--legacy-peer-deps` - which this repository's own
  workspaces and every known downstream need, because `@abp/ng.theme.shared`'s `@swimlane/ngx-datatable`
  caps its Angular peer at 20 - guaranteed it would not be installed even where the peer was declared.
  It is now a real `dependency` of both packages, listed in each `ng-package.json`'s
  `allowedNonPeerDependencies` so ng-packagr accepts a bundled non-peer edge as deliberate. The rule
  this restores: a package belongs in `peerDependencies` only when the consumer is guaranteed to have
  it already; otherwise it is a `dependency`.

  `ng-zorro-antd` and `@angular/cdk` reach these packages by the same single route - through
  `@abp/ng.components`, which depends on `ng-zorro-antd`, which depends on `@angular/cdk` - and by that
  rule they are irregular in the same way. They are **deliberately left as peers**. `@abp/ng.components`
  pins `ng-zorro-antd` at `~21.0.0-next.1`, that is `<21.1.0`, while these packages declare `^21.0.0`
  and current hosts run `21.3.3`; there is no version that satisfies both, so a resolver settles it by
  installing both copies. That is already the state of this repository's own workspaces - `21.3.3` at
  the root, `21.0.2` nested under `@abp/ng.components` - and it is a consequence of that upstream pin,
  not of how these packages declare anything. Two copies of ng-zorro are two module-scoped
  `NZ_CONFIG` / `NzConfigService` injection tokens, the identity split described for
  `@dignite/ng.flex-fields` in [#211](https://github.com/dignite-projects/abp-modules/issues/211):
  `provideNzConfig()` and `provideNzI18n()` at the root configure the copy these packages' own controls
  use, and not the one ABP's `abp-tree` sees. Moving them to `dependencies` would not merge the copies,
  only move the choice of the root one away from the host - so the host keeps it, and both READMEs now
  state that these two must be declared by the consumer and what a range wider than `<21.1.0` costs.
  `@abp/ng.components` had no such conflict - one range, one source - which is what makes moving that
  one a pure gain.

## [10.0.0-rc.14] - 2026-09-04

### Fixed

- **Every tag-triggered release since the npm Trusted Publishing migration (`v10.0.0-rc.12` and
  `v10.0.0-rc.13`, twice for the latter) crashed inside `release.yml` before the job ever published
  anything to NuGet.org or npm.** The job's "Setup Node.js" step set `registry-url` (needed for the
  later OIDC-based npm publish steps), which makes `actions/setup-node@v7` write an
  `//registry.npmjs.org/:_authToken=${NODE_AUTH_TOKEN}` placeholder into a generated `.npmrc`.
  Nothing in the job has set `NODE_AUTH_TOKEN` since npm publishing became OIDC-based — npm's own
  Trusted Publishing doesn't use it at all — but Yarn Classic (1.x, used by all three Angular
  workspaces) eagerly substitutes every env-var placeholder in its resolved config on **every**
  invocation, not just registry-touching ones, and throws when one is unset. A first fix dropped the
  `cache: yarn` step option that was triggering this during its internal `yarn cache dir` probe; that
  only moved the crash to the next yarn command Setup Node's cache change didn't touch — the job's
  very first real `yarn install` — because the placeholder-bearing `.npmrc` stays active for every
  step for the rest of the job, not just the one that wrote it. The actual fix moves `registry-url`
  off the early "Setup Node.js" step entirely, onto a second `actions/setup-node@v7` call added right
  after the last yarn command in the job (before the GitHub Packages and npmjs publish steps that
  need it) — so no yarn command ever sees that `.npmrc` in the first place. `ci.yml`'s
  identical-looking "Setup Node.js" step never hit any of this because it never sets `registry-url`.
- **`release.yml`'s "Publish pre-release Angular packages to GitHub Packages" step had no `--tag`
  on its `npm publish` call**, and npm refuses to publish a pre-release version (every version this
  repository has ever shipped so far) without one explicitly stated. Unlike the crash above, this
  one only broke once the job got far enough to actually reach it. Added the same
  `--tag '${{ steps.channel.outputs.npm-tag }}'` the sibling npmjs-publishing step already used.
- **The GitHub Packages pre-release step appended its own registry/auth lines to the same
  `.npmrc` `actions/setup-node`'s `registry-url` wrote for npmjs.org OIDC** (`$NPM_CONFIG_USERCONFIG`,
  read by "Publish tagged Angular packages to npm" later in the same job) — corrupting npm's parsing
  of that shared file and breaking OIDC's registry detection there with a garbled `ENEEDAUTH` error
  quoting both registries concatenated together. The GitHub Packages step now writes its auth to its
  own scratch file, passed via `--userconfig` on just its own `npm publish` calls, leaving the
  OIDC-relevant file untouched for the rest of the job.

- **The two Angular adapter packages declared their intra-repo siblings at a stale range, so
  consumers could end up with two copies of `@dignite/ng.flex-fields` and no working field types.**
  `flex-fields-ckeditor` and `flex-fields-file-explorer` both still asked for `^10.0.0-rc.4` while
  every package in the repository shipped `10.0.0-rc.13`. That range admits an older sibling, so a
  resolver is free to satisfy it with one rather than deduplicating against the copy already at the
  root — and Yarn Classic does exactly that whenever npm's `latest` tag sits on an older version
  than the newest published one, which is the case today (`latest` is `10.0.0-rc.11`, `next` is
  `10.0.0-rc.13`). The result is not wasted bytes: Angular DI keys off object identity and
  `FLEX_FIELD_TYPES` is a module-scoped `InjectionToken`, so two copies are two distinct DI keys —
  `provideCKEditorFieldType()` registers into one while `FieldTypeResolver` reads the other, and
  every field type appears unregistered at runtime with nothing having failed at install or build
  time. All three ranges now track the release version, and
  `.github/scripts/verify-version-lockstep.ps1` — already the release's version-lockstep gate —
  additionally fails the release if any `@dignite/*` dependency or peer dependency of a published
  Angular package names anything other than the version being released, so this cannot drift again.
  Note that clearing the duplicate for consumers still on `latest` also needs the `latest` dist-tag
  moved off `10.0.0-rc.11`. See [#211](https://github.com/dignite-projects/abp-modules/issues/211).
- **`release.yml` now verifies, after publishing, that a Yarn Classic install of the just-released
  packages resolves exactly one copy of each.** The packed-tarball smoke tests already in the
  workflow install with **npm**, which deduplicates; all three Angular workspaces here and every
  known downstream use **Yarn Classic**, which does not. That gap is what let the duplicate above
  reach consumers with every existing check passing. `build/verify-npm-single-copy.mjs` closes it by
  checking the *resolved* outcome rather than the manifests — so a duplicate arriving by some other
  route (a transitive `@dignite/*` edge, a dist-tag that makes a caret resolve backwards) is caught
  too. It runs after the npm publish step, since it resolves real versions from npmjs that do not
  exist until then; a failure therefore cannot un-publish anything, it stops the draft GitHub Release
  and reports that the just-published set does not install cleanly.
- **A pre-release now also takes npm's `latest` dist-tag, for as long as no stable release exists.**
  `release.yml` published every pre-release under `next` alone and reserved `latest` for a stable
  version — of which there is none yet on the 10.x line. `latest` was therefore left wherever it
  happened to land before that convention took hold (`10.0.0-rc.11`, while `10.0.0-rc.13` was the
  newest published), so a bare `npm install @dignite/ng.flex-fields` handed out a version two releases
  behind, and Yarn Classic resolved intra-repo ranges backwards onto it — the second of the three
  conditions behind [#211](https://github.com/dignite-projects/abp-modules/issues/211). The tag had
  been corrected by hand, but the workflow would have re-created the gap at the next pre-release.
  `build/resolve-npm-dist-tag.mjs` now decides it: a stable version always takes `latest`, and a
  pre-release takes it too **only while the registry holds no stable release of these packages**,
  falling back to `next` from the moment one exists. Reading the registry rather than flipping a flag
  means the rule retires itself when `10.0.0` ships, instead of silently moving consumers off a stable
  release onto a later `10.1.0-rc.1`. The `channel` output is unchanged and still means "is this a
  pre-release" for the GitHub Packages mirror and the draft Release's own flag.

#### flex-fields

- **`@dignite/ng.flex-fields`' `ng-zorro-antd` peer range rejected every release after `21.0.x`.** It
  was `~21.0.0-next.1`, which expands to `>=21.0.0-next.1 <21.1.0-0` — so `21.1.0` and everything
  since, up to the current `21.3.3`, failed the peer. Under npm 7+ that is an `ERESOLVE` install
  error rather than a warning; Yarn Classic downgrades it to a warning, which is why it had not
  surfaced. The range dated from when `21.0.0-next.x` was the newest thing published and was never
  revisited after `21.0.0` went stable. It is now `^21.0.0`, matching how `.github/dependabot.yml`
  already reasons about this package (its major tracks Angular's, so majors are ignored and the
  `21.x` line is meant to be tracked). `flex-fields/angular`'s own dependency moves to `^21.3.3`
  alongside it, so the workspace develops against a version the library claims to support — it had
  been pinned to the same capped range and stuck on `21.0.2` while `file-storing/angular` was already
  on `21.3.3`. See [#220](https://github.com/dignite-projects/abp-modules/issues/220).

- **`CKEditorControlComponent`'s theme bridge now resolves against `<body>` as well as `<html>`.** A
  custom property's `var()` references are resolved against the element the property is declared on,
  not against wherever it is eventually consumed, so the bridge's `:root` declarations could only
  ever see theme variables set on `<html>`. A host that marks its dark theme on `<body>` instead —
  `data-bs-theme="dark"` on the body element, say — left the bridge resolving the light values, and
  the editor stayed light while the rest of the page went dark. Both blocks are now declared on
  `:root, body`, so either placement works. Coverage is unchanged: CKEditor's UI, including the
  balloon/dropdown wrapper it appends directly under `<body>`, is entirely inside the body subtree.
- **The `Select` and `Tree` field controls’ dropdown panels now follow a dark host.** Both painted
  their panel with `var(--lpx-content-bg, #fff)`, but `--lpx-content-bg` is a **full** LeptonX token
  (`@volosoft/ngx-lepton-x`: `#f0f4f7` light, `#121212` dark). LeptonX **Lite** — `@volo/ngx-lepton-x.lite`,
  which `@abp/ng.theme.lepton-x` wraps — never defines it: it ships 11 `--lpx-*` tokens and this is
  not one of them. The chain therefore fell straight through to the literal `#fff` and the panel
  stayed white in every theme, while the options’ own `color: var(--bs-body-color)` did follow the
  host — light grey text on white, unreadable. Both now fall back to `--bs-secondary-bg` first
  (`#e9ecef` light, `#343a40` dark in Lite), the Bootstrap 5.3 “one step off the body surface” token
  that every Bootstrap-based theme defines at `:root` and redefines under `[data-bs-theme=dark]` —
  the same chain shape `flex-fields-ckeditor` already uses for `--ck-color-base-foreground`. Light
  mode moves from pure white to `#e9ecef`.
- **The `Select` field's multi-select tags were near-illegible in a dark host.** ng-zorro hardcodes
  the tag's entire chrome — `background: #f5f5f5`, `border: 1px solid #f0f0f0`, and
  `rgba(0, 0, 0, 0.45)` on the remove icon — while the tag's label does follow the host, because this
  file already sets `color: inherit` on `.ant-select`. The result in dark mode was a light label on a
  near-white chip with an invisible “×”. All three now map to `--bs-secondary-bg`,
  `--bs-border-color` and `--bs-secondary-color`.

- **The `Tree` field gave no feedback about which node was selected in single-select mode.**
  `abp-tree` marks selection with a `.selected` class of its own — it renders
  `<div [class.selected]="isNodeSelected(node)">` as the node wrapper's direct child — and never binds
  `nzSelectedKeys`, so ng-zorro's `.ant-tree-node-selected` is never applied at all, and `abp-tree`
  attaches no styling to `.selected`. Clicking a node therefore changed nothing on screen, and
  reopening a saved value gave no indication of which node it held. (Multi-select was unaffected: it
  renders a checkbox per node.) The selected node now takes the same `--lpx-brand` / white treatment
  as the `Select` field's chosen option. Node hover, which neither package had touched and which
  ng-zorro paints `#f5f5f5`, now uses `--bs-secondary-bg`.

- **The `Tree` field's search dropdown had a hardcoded `rgba(0, 0, 0, 0.12)` border**, invisible
  against the dark panel. Now `--bs-border-color`, like every other Bootstrap-based control.
- **The `Tree` field's node editor (`ff-tree-config`, the “Nodes” panel in field configuration)
  carried no styles of its own**, so every ng-zorro default came through unmodified — all of it
  hardcoded light-mode. Hovering a node painted a `#f5f5f5` bar under text that follows the host
  (`abp-tree` sets `.ant-tree { color: inherit }`), so in a dark host the row went light-on-white and
  the label disappeared; the post-click “active” node did the same. `.ant-tree`'s own opaque
  `background: #fff` was there too, which would have shown the whole editor as a white box in a
  genuinely dark host. Hover and active now use `--bs-secondary-bg` and the tree background is
  transparent, matching what the `Tree` picker already did.

- **A selected option in the `Select` field's dropdown rendered white text on the panel background
  once the mouse left it.** The rule meant to give selected options a `--lpx-brand` fill carried
  `!important` on its colour but not on its background, while the `.ant-select-item` rule below it
  zeroes every option background with `!important` — and `!important` beats specificity, so the
  background never applied and the white text always did. It was invisible for as long as the panel
  was `#fff` and became merely illegible once the panel started following `--bs-secondary-bg`. The
  hover rule has `!important` on both halves, which is why hovering a selected option looked correct
  and moving off it did not. Selected options now take no colour of their own and read exactly like
  unselected ones, with ant-design's own checkmark as the indicator: in a multi-select several
  options are selected at once, and filling each of their rows competes with the hover state rather
  than adding information.
## [10.0.0-rc.13] - 2026-09-03

### Added

- **`build/check-angular-package-deps.mjs`, a CI gate that fails when a built library imports a
  package its own `package.json` does not declare.** ng-packagr marks every bare specifier it does
  not bundle as an external but never checks that the external is declared, so a library could
  publish a bundle asking for a package it never named — nothing failed at build time, nothing
  failed at `npm install`, and the consumer met an unresolvable specifier the first time they built
  their own app. The existing `smoke-test-angular-package.mjs` cannot see this class of defect: it
  seeds the throwaway consumer with the demo app's dependency list, so every undeclared package is
  already installed before the compile it verifies. All five libraries were failing this check when
  it was written.

### Changed

- npmjs publishing switched from a long-lived `NPM_TOKEN` secret to npm Trusted Publishing (OIDC);
  each of the five Angular packages now has its own Trusted Publisher configured on npmjs.com.
  `@dignite/ng.flex-fields-ckeditor` is published to npmjs on tagged releases for the first time.
- The GitHub Packages pre-release npm mirror now tolerates re-runs at an unchanged version,
  treating "already published" as a skip instead of failing the step.
- **Dependencies a consumer cannot already have moved from `peerDependencies` to `dependencies`.**
  Every ABP 10.5 + Angular 21 host is forced to set `legacy-peer-deps` — `@abp/ng.theme.shared`
  pins `@swimlane/ngx-datatable@~22.0.0`, whose Angular peer range stops at 20 — and under that
  flag npm does not install peer dependencies at all. A peer the host does not already have is
  therefore an unresolvable import discovered at the consumer's build, with nothing in
  `npm install` to warn them. The evidence that the peer model never worked here: all five known
  consumers (`site`, `vault-extract`, and this repository's own three demo apps) had hand-copied
  the same peer lists into their own `package.json`, at three different `ng-zorro-antd` ranges.
  Moved to `dependencies`: `ckeditor5`, `@ckeditor/ckeditor5-angular`, `marked` and
  `@dignite/ng.flex-fields` in `@dignite/ng.flex-fields-ckeditor`; `@dignite/ng.flex-fields` and
  `@dignite/ng.file-explorer` in `@dignite/ng.flex-fields-file-explorer`; `@microsoft/signalr` in
  `@dignite/ng.notification-center`. Packages an ABP Angular host has by construction
  (`@angular/*`, `rxjs`, `@abp/ng.*`) or through ABP's own dependency tree (`@ngx-validate/core`,
  `ng-zorro-antd`, `@angular/cdk`, `@ng-bootstrap/ng-bootstrap`, `@swimlane/ngx-datatable`) stay
  peer dependencies: there the declaration states the tested range without risking a second copy
  of a singleton in the consumer's tree. Consumers that were carrying those packages by hand can
  drop them; consumers that were not no longer have to discover them.

### Fixed

- **Every Angular package imported at least one package it did not declare.**
  `@ngx-validate/core` in `@dignite/ng.flex-fields`, `@dignite/ng.flex-fields-ckeditor` and
  `@dignite/ng.flex-fields-file-explorer`; `rxjs` in `@dignite/ng.flex-fields-ckeditor` and
  `@dignite/ng.notification-center`; `@swimlane/ngx-datatable` in `@dignite/ng.file-explorer`; and
  `@ckeditor/ckeditor5-integrations-common`, whose `EditorRelaxedConstructor` appears in
  `@dignite/ng.flex-fields-ckeditor`'s published `.d.ts`, so a consumer compiling without
  `skipLibCheck` needed it resolvable. All now declared; the new dependency check keeps them so.
- **`flex-fields-ckeditor` was built and published by `release.yml` but never built by `ci.yml`**,
  so a pull request that broke the CKEditor adapter stayed green until tag time. CI now builds it
  alongside the other libraries.

#### flex-fields

- **`CKEditorControlComponent` now follows the host theme, including dark mode.** CKEditor 5 ships a
  single stock light palette (`--ck-color-base-background`/`-foreground`/`-border`/`-text` in its own
  `:root`); the control set none of them, so in a dark-themed host the editor stayed light unless the
  host added its own bridge (only `site` had one). The control now maps those four tokens, plus the
  two hardcoded toolbar-button hover/active fills, to the host's theme variables — a LeptonX token
  when present, falling back to the Bootstrap 5.3 token every ABP Angular theme ships, then to
  CKEditor's stock literal — so a host on full LeptonX (`@volosoft/ngx-lepton-x`) or on a plain
  Bootstrap 5.3 dark theme no longer needs a `--ck-color-base-*` bridge of its own. Two limits are
  worth knowing. The editor's main chrome — toolbar, balloon and dropdown panels, list and input
  surfaces — is `var()`-derived from those four tokens and re-themes with them, but roughly 60 other
  `--ck-color-*` tokens are hardcoded literals in `ckeditor5.css` and stay light regardless. And
  LeptonX **Lite** (`@volo/ngx-lepton-x.lite`, which `@abp/ng.theme.lepton-x` wraps) ships no dark
  theme at all — one fixed look, no theme-switching code in the package — and pins `--lpx-card-bg` to
  a constant `#ffffff`, so on Lite the editor stays white, matching Lite's own white cards. A host
  hand-rolling a dark mode on top of Lite has to override `--lpx-card-bg` itself.

## [10.0.0-rc.11] - 2026-08-31

### Fixed

#### flex-fields

- **`NumberControlComponent` no longer changes its control's value type when it truncates.** Typing more
  decimal places than `Number.Decimals` allows made `onInput` write the truncated value back as a
  **string**, while every other keystroke leaves Angular's own `NumberValueAccessor` value — a number — in
  place. A consumer serializing the form straight to JSON therefore sent `"1.23"` where its contract said
  number, but only for the values that overflowed the configured precision, which made it look
  intermittent. The truncated value is now patched as a number; a non-finite result (an empty or
  lone-sign integer part) still falls back to the raw string rather than silently becoming `0`.

## [10.0.0-rc.7] - 2026-08-30

> `10.0.0-rc.6` also exists on NuGet.org with identical package contents — that release run failed
> at the npm publishing step (the `NPM_TOKEN` granular access token had no package/scope
> permissions), so none of the four public Angular packages were ever published under it. `rc.7`'s
> own tagged run (and `rc.11`'s) also failed at that step, this time because the replacement token
> still required a 2FA one-time password — so no `10.0.0-rc.*` Angular package after
> `@dignite/ng.notification-center@10.0.0-rc.3` reached npmjs from CI. The `rc.11` tarballs are
> published manually out-of-band, and CI moves to npm Trusted Publishing going forward.

### Changed

#### flex-fields

- **Breaking: `IFlexField.Description` no longer has a length cap.** `FlexFieldConsts.MaxDescriptionLength`
  (256 characters) is removed; the EF Core column now maps to the provider's unbounded text type instead
  of a fixed-width one. `Description` is sometimes used as free-form prompt text for an AI rather than a
  short blurb, which routinely exceeded the previous cap. Hosts add their own migration to widen the
  column next time they touch the model.

#### file-storing

- `Dignite.FileExplorer.HttpApi.Client` now registers `AddStaticHttpClientProxies` instead of the unused
  dynamic `AddHttpClientProxies` path — the generated `ClientProxies/` code already shipped but wasn't
  being used.

#### notifications

- `Dignite.NotificationCenter.HttpApi.Client` switches from dynamic to static proxies
  (`AddStaticHttpClientProxies`), gaining generated `ClientProxies/` code and a new
  `NotificationCenterRemoteServiceConsts` type (mirroring `FileExplorerRemoteServiceConsts`) so the
  controllers' `RemoteService`/`Area` names and the client module's `RemoteServiceName` reference one
  constant instead of matching hardcoded strings by hand.

### Fixed

#### flex-fields

- CKEditor's editable content now themes correctly in dark mode — `--ck-content-font-color` is
  repointed at the same base-text token the control already sets, instead of inheriting CKEditor 5's
  own light-mode default.
- The Select field type's dropdown options are now readable in dark mode — an ng-zorro CSS specificity
  tie was leaving idle (non-hover, non-selected) options on ng-zorro's own light-mode colors regardless
  of theme.
- The Tree field type's picker (inline control and search dropdown) no longer paints an opaque white
  background in dark mode.
- `ConfigureAwait.Fody` is now actually applied to the CKEditor and FileExplorer integration projects —
  see file-storing below for why this matters.

#### file-storing

- `ConfigureAwait.Fody` is now actually applied to the core and file-explorer projects — they were
  missing the per-project `FodyWeavers.xml` opt-in file the repo-wide convention requires, so
  `.ConfigureAwait(false)` wasn't being woven into their `await`s. A host running these modules on a
  `SynchronizationContext` (WPF/WinForms/Blazor Server/classic ASP.NET) could previously deadlock.

## [10.0.0-rc.5] - 2026-08-17

### Added

#### flex-fields

- **New Angular package `@dignite/ng.flex-fields`.** The Angular half of FlexFields, migrated from
  `Dignite.Abp.DynamicForms`' `@dignite-ng/expand.dynamic-form` in the `dignite-abp` repository and
  renamed in step with the C# naming map. Ships config / control / view / search components for all
  six field types, the `FieldTypeResolver` registry, and `provideFlexFields()`.
  - The six **registration keys** (`Text`, `Number`, `DateTime`, `Select`, `Boolean`, `Tree`) and
    every configuration key are stored values, not class names, and match the server byte-for-byte —
    a test asserts each one. These were renamed once more during development, from the pre-rename
    `Dignite.Abp.DynamicForms` stragglers (`TextEdit`/`NumericEdit`/`DateEdit`/`Switch`/`TreeView`) to
    align with the C# type names, before this package's first release — see `flex-fields/CLAUDE.md`
    for the full mapping.
  - Registering a field type is now a typed `InjectionToken` multi-provider instead of a bare
    `'MERGED_FORM_CONFIG'` string token that the library never provided and a `forRoot()` that
    silently discarded its argument. Several packages can register independently, and a later
    registration overrides a built-in of the same name.
  - Components are standalone; the `dynamic-form` NgModule is gone.
  - The stale embedded copy of the FileExplorer API proxy was dropped — it was dead code, and it
    coupled two modules that are meant to be independently installable.
  - The tree designer's value suggestion is now an optional `FLEX_FIELD_SLUG_GENERATOR` token with a
    plain slug default, rather than a hard-wired `pinyin-pro` dependency in a general-purpose module.
  - `DateTime` still has no search component, as before. Known gap, not a regression.
  - `Dignite.Abp.FlexFields.Abstractions`'s `FlexFields` localization resource gains two keys the
    migrated tree designer's node-key validator needs: `Validate:InvalidNodeValue`,
    `Validate:NodeValueAlreadyExists` (en/ja/zh-Hans/zh-Hant).
- **New `Dignite.Abp.FlexFields.Web` package.** `<flex-field-view>`/`<flex-field-search>` TagHelpers
  plus default views for the six built-in field types — the server-side (Razor) counterpart to the
  Angular library's `<ff-flex-field-view>`/`<ff-flex-field-search>`. Zero-IO by design: TagHelpers
  render an already-resolved `FlexFieldValue`, the same way the kernel has no application service of
  its own to look one up with, so assembling it is the host's job. No config/control TagHelpers —
  display and search only. `<flex-field-search>` renders inputs only; turning what gets submitted into
  a `FlexFieldQueryCondition` stays the host's job, same as the Angular library.
- **New `Dignite.Abp.FlexFields.FileExplorer.Web` package.** The FileExplorer bolt-on field type's own
  view — file name/size/MIME type/link, read straight out of the value the Angular picker already
  denormalized at pick time. No IO, no reference to `Dignite.FileExplorer`, and no search partial
  (`FileExplorerFieldType.IndexValueType` is `null`). Depending on it alone pulls in both the field
  type and `Dignite.Abp.FlexFields.Web`.

### Changed

- **Repository merged.** `dignite-projects/abp-file-storing` and `dignite-projects/abp-notifications`
  are now developed and released together from `dignite-projects/abp-modules`, as `file-storing/`
  and `notifications/`. **No PackageId, root namespace, or `AssemblyVersion` changed** — this is
  transparent to NuGet and npm consumers. Both modules' full commit histories are preserved.
- **Versioning is now lockstep across both modules** (previously each repository versioned
  independently): one `<Version>` in the root `Directory.Build.props`, one `v*` tag, one release
  pipeline covering all 25 NuGet packages and both Angular packages.
- `PackageProjectUrl` / `RepositoryUrl` and both Angular packages' `homepage` / `repository.url` now
  point at `dignite-projects/abp-modules`.

#### file-storing

- The Angular package `@dignite-ng/expand.file-explorer` jumps from `10.0.0-rc.1` to the lockstep
  version, catching it up to the .NET packages it ships alongside.
- **Breaking: the Angular package is renamed `@dignite-ng/expand.file-explorer` →
  `@dignite/ng.file-explorer`**, matching `@dignite/ng.notification-center` and
  `@dignite/ng.flex-fields` so all three npm packages share one convention. Both entry points move
  (`@dignite/ng.file-explorer` and `@dignite/ng.file-explorer/config`); nothing else about the
  package changed. Update your imports and `package.json`. The old name is not deprecated-with-a-
  shim, it simply stops receiving updates — acceptable because the only versions ever published
  under it are `10.0.0-rc.*` pre-releases.

---

> **History before the merge.** The releases below were published from the standalone
> `dignite-projects/abp-notifications` repository and cover the **`notifications/` module only**.
> The `file-storing/` module had not published a release at the time of the merge, so it has no
> entries before this point.

## [10.0.0-rc.3] - 2026-07-23

> This pre-stable line explored a much larger feature surface (per-recipient delivery reliability with leases /
> retries / dead-lettering / force-delivery, per-user delivery preferences + quiet hours, large-audience broadcast
> orchestration, payload schema-versioning + upcasters, opt-in definition payload/entity contracts, a replaceable
> batch eligibility evaluator + trusted-recipient bypass API, distribution metrics, a prepared multi-job
> fan-out, and a scheduled retention/lifecycle-cleanup worker + options) and then **cut all of it before release**
> as over-engineering for a best-effort in-app notification module. None of it shipped, so it is not documented as removed below. The module's positioning is deliberately
> "best-effort in-app notifications": delivery is fire-once (the inbox row is authoritative), and distributed-systems
> machinery (delivery reliability/retries, broadcast jobs, schema-evolution upcasters) is intentionally absent.

### Added

- MongoDB integration with ABP's distributed event outbox and inbox through `UseNotificationCenterMongoDbOutbox()`,
  using ABP-compatible event-box collections with query indexes and sharing atomic commit/rollback tests with EF
  Core. The transactional outbox requires a transaction-capable MongoDB 4.0+ replica set and transactional ABP
  units of work.
- Scoped subscription application/REST contracts that round-trip the stable entity type and ID, while retaining the
  name-only methods as definition-wide compatibility wrappers. MVC and Angular subscription UIs submit the complete
  scope.
- Tolerant notification-data reads: `INotificationDataSerializer.Deserialize(json)` returns a safe
  `UnsupportedNotificationData` placeholder for unknown or malformed payloads instead of throwing, so one bad
  historical row cannot fail a whole inbox page. MVC and Angular render it with a localized fallback. (A
  strict-vs-tolerant read-mode switch existed briefly pre-release; it was cut because every real read boundary
  already chose tolerant, so strict mode had no caller. Not documented as removed below, per the note above.)
- A bounded recipient pipeline: `INotificationStore.GetSubscriptionUserIdsAsync` keyset paging plus bounded inbox
  multi-insert. Explicit fan-outs above `NotificationDistributionOptions.DirectDistributionUserThreshold` run on a
  single background job whose distributor batches recipients internally (`RecipientBatchSize`).

### Changed

- **Breaking NotificationCenter package-family rename before 10.0.0 stable.** The optional Notification Center
  packages, namespaces, and module class names now use `Dignite.NotificationCenter*` instead of
  `Dignite.Abp.NotificationCenter*`. This is a naming-only change with no functional behavior change.
- **Breaking application/domain API alignment before 10.0.0 stable.** Current-user inbox services are now
  `IUserNotificationAppService` / `UserNotificationAppService`, and `GetCountAsync` is `GetNotificationCountAsync`.
  Pass-through manager interfaces and `UserNotificationManager` were removed; application reads now use
  `INotificationStore` while the concrete `NotificationSubscriptionManager` owns validated subscription mutation.
  REST routes are unchanged.
- **Breaking options split before 10.0.0 stable.** The catch-all `NotificationOptions` type was replaced by
  `NotificationDefinitionRegistration` (provider registration) and `NotificationDistributionOptions` (inline/background
  threshold + `RecipientBatchSize`, capped by `MaxBatchSize` = 10,000, validated on startup). Custom constructors
  and `IOptions<T>` consumers must adopt the responsible option type and be recompiled; no database migration.
- **Breaking notifier contract.** `INotificationNotifier` is the sole channel execution contract: `Name` plus
  cancellation-aware single-recipient `DeliverAsync`. Delivery is best-effort — `DeliverAsync` returns `Task`
  (no result type); a notifier skips a recipient by returning, and a throw is logged and dropped by the Core
  handler, not retried. The Core-owned distributed-event handler adapts transport, so channel plugins do not
  implement an event-handler interface.
- **Breaking distributed-event contract.** The default distributor publishes single-recipient/channel
  `NotificationDeliveryRequestedEto` (wire name `Dignite.Abp.Notifications.NotificationDeliveryRequested`) instead of
  the legacy batched aggregate event. Quiesce publication, drain old aggregate events, upgrade consumers, then producers.
- **Breaking distributed-event payload envelope.** `NotificationDeliveryRequestedEto.Data` (a live, abstract
  `NotificationData`) is replaced by `DataJson`: the payload pre-serialized as discriminator-tagged JSON via
  `INotificationDataSerializer` at the distributor publish boundary and hydrated at the notifier boundary
  (`NotificationPayload.FromRequest(request, dataSerializer)`; both built-in notifiers inject the serializer). ABP's
  event bus — including the transactional outbox/inbox — serializes ETOs with plain System.Text.Json and no
  app-level options, so an abstract member cannot round-trip the box; a pre-serialized string survives any transport
  and stays readable for non-.NET consumers. Recompile channel plugins and rebuild custom notifiers against the new
  member. The rename is deliberate: pre-upgrade outbox rows (whose `Data` was written lossily) drain as a null
  payload under the new contract instead of poisoning the sender with a type mismatch.
- **Breaking pre-stable naming cleanup.** The single-recipient payload type `NotificationDelivery` (added in
  10.0.0-preview.2) is renamed to `NotificationPayload`, and its factory `FromWorkItem` to `FromRequest`, so the
  "delivery" vocabulary names only the act while the payload type reads as content. The delivery event's wire name is
  normalized from `Dignite.Abp.Notifications.NotificationDeliveryWork` to
  `Dignite.Abp.Notifications.NotificationDeliveryRequested` to match its `NotificationDeliveryRequestedEto` contract
  name. Recompile channel plugins/remote consumers and drain any in-flight events on the old wire name before upgrading.
- **Breaking for MongoDB context implementers.** `INotificationCenterMongoDbContext` extends ABP's `IHasEventInbox`
  and `IHasEventOutbox`. Consumer-owned implementations must expose and model the two ABP event collections and
  configure both boxes against their custom context. Notification business records require no backfill or rename.
- **Breaking behavior for callers.** An explicit `userIds` array no longer bypasses a notification definition's
  permission and feature requirements: `PublishAsync` filters explicit and subscription-derived recipients through
  the same `INotificationDefinitionManager.IsAvailableAsync` check, in the notification's tenant or host context.
- **Breaking behavior for direct distributor callers.** `NotificationInfo.TenantId` is authoritative for
  subscription lookup, eligibility, persistence, and event/outbox publication. `null` always means host and never
  falls back to an ambient tenant, so tenant-side callers of `INotificationDistributor` must set it explicitly.
- Subscription-driven distribution treats a definition-wide subscription as a fallback for every entity and combines
  it with an exact entity subscription without delivering twice to the same user. Subscription uniqueness uses
  non-null, ordinal identity keys across EF Core and MongoDB; existing databases need a host-owned backfill and
  index migration as documented in the README.

### Fixed

- Hosts routing the distributed event bus through ABP's transactional outbox (`UseNotificationCenterEfCoreOutbox()`
  / `UseNotificationCenterMongoDbOutbox()`) could never deliver to SignalR or email: draining
  `NotificationDeliveryRequestedEto` from the box threw `System.NotSupportedException` on the abstract `Data`
  member (ABP deserializes outbox/inbox payloads with plain System.Text.Json), and the write side had already
  dropped the derived payload fields. Fixed by the `DataJson` envelope above; the shared outbox contract tests now
  drain both event boxes and assert the concrete payload survives. ([#118](https://github.com/dignite-projects/abp-notifications/issues/118))
- Notification definition names and `NotificationData` discriminators use explicit ordinal, case-sensitive
  registration and lookup. Conflicting registrations fail during application startup with both providers or CLR
  mappings identified instead of silently replacing an earlier value. Definition providers are discovered across
  module assemblies and duplicate registrations of the same provider execute once.
- Consistent explicit-recipient semantics across inline and background distribution: `null` resolves subscriptions,
  an empty list is a no-op, and duplicate explicit user IDs are normalized before threshold selection, persistence,
  and channel delivery.
- `NotificationDistributionJob` had two public `ExecuteAsync` overloads, so ABP's `BackgroundJobExecuter` (which
  resolves a job's execute method by name via reflection) threw `AmbiguousMatchException` for every
  background-dispatched distribution — any publish without explicit `userIds` (subscription-driven notifications,
  or a large explicit fan-out) silently failed while the triggering AppService call still returned 200/204. The
  second overload is renamed to `ExecuteWithCancellationAsync`. Fixing that exposed the job never opening a Unit of
  Work, so `NotificationStore.GetSubscriptionUserIdsAsync` threw `ObjectDisposedException` on the `DbContext`; the
  job now wraps distribution in `UnitOfWorkManager.Begin(requiresNew: true)` so the notification insert, inbox
  rows, and outbox event write commit atomically.

## [10.0.0-rc.2] - 2026-07-16

### Changed

- Renamed the Angular package to `@dignite/ng.notification-center`, making the UI framework
  explicit and leaving room for parallel React or other client packages.
- Clarified that npm requires every package to have a `latest` dist-tag, so a package whose first
  public version is a pre-release temporarily exposes that version as both `next` and `latest`.

## [10.0.0-rc.1] - 2026-07-16

### Added

- Added CI coverage for the Angular library and production demo build.
- Added isolated consumption smoke tests for all 15 packed NuGet packages and both Angular package
  entry points.

### Changed

- Synchronized the Angular package version with the NuGet release version.
- Renamed the Angular package to `@dignite/abp-notification-center` so all Dignite npm packages
  share the `@dignite` organization scope.
- Tagged pre-releases are now published publicly to NuGet.org and npm in addition to the existing
  GitHub Packages preview feed.
- Replaced the long-lived NuGet API key with NuGet.org Trusted Publishing and a short-lived OIDC
  credential issued to the tagged release workflow.
- Expanded the README with package installation commands, a compatibility table, and migration
  guidance for legacy 3.x consumers.

## [10.0.0-preview.2] - 2026-07-10

> `MAJOR` tracks the targeted ABP Framework version, so a breaking change to this module's own
> contracts arrives in a `MINOR` bump. Entries below marked **Breaking** require action when
> upgrading.

### Added

- Added `Dignite.Abp.Notifications.Emailing.Identity`, an optional ABP Identity-backed
  `IEmailNotificationAddressResolver` for the Emailing notifier.
- `NotificationDeliveryRequestedEto` and `NotificationDelivery` now carry the notification's `EntityTypeName` and
  `EntityId`, so a notifier can identify the business entity a notification is about without depending on
  Core.
- Added `NotificationEmailContentProvider<TData>`, a base class that narrows `NotificationData` once so an
  implementer cannot forget the type guard and accidentally claim every notification.
- Email address resolvers can now return an optional recipient culture; email content is built inside that culture
  and falls back to `NotificationEmailOptions.DefaultCulture`.

### Changed

- Changed email address resolution to use `EmailNotificationAddressResolveContext`, making
  `TenantId` explicit to local and remote resolver implementations.
- **Breaking.** `NotificationEntityIdentifier` now takes `(string entityTypeName, string entityId)` instead of
  `(Type entityType, object entityId)` — pass a short, stable name such as `"Demo.Order"`. Persisted
  `EntityTypeName` values are therefore no longer `Type.FullName`, so subscription rows and
  `NotificationCenterWebOptions.EntityLinkResolvers` keys written in the old format stop matching.
- **Breaking.** `IEmailNotificationAddressResolver` gained an `Order` member, and `GetEmailOrNullAsync` returns
  `Task<EmailNotificationAddress?>` rather than `Task<string?>`. Resolvers now form an ordered chain in which the
  first non-null address wins.
- **Breaking.** `IdentityEmailNotificationAddressResolver` no longer declares `[Dependency(ReplaceServices = true)]`.
  It joins the chain at `NotificationEmailProviderOrders.BuiltInFallback`, so an application resolver composes with
  it rather than displacing it.
- **Breaking.** Renamed `NotificationEmailContentProviderOrders` to `NotificationEmailProviderOrders`, which now
  orders both the content-provider and the address-resolver chains.
- **Breaking.** `NotificationEmailBuildContext`'s constructor gained a `cultureName` parameter.
- Documented that atomic persist-and-publish, and deduplication of a redelivered event, require the host to enable
  ABP's transactional outbox — `UseNotificationCenterEfCoreOutbox()` on EF Core. The MongoDB provider wires neither.
  No behaviour changed; the previous comments and README described a guarantee that was conditional.

### Fixed

- `EmailNotifier` no longer aborts the whole delivery when a recipient's culture name cannot be parsed. It falls
  back to `NotificationEmailOptions.DefaultCulture` and then to the ambient culture, logging a warning for each
  rejected value. Previously a single malformed per-user language setting threw out of the event handler, so every
  recipient after it received nothing and a redelivery re-mailed the ones before it. As part of this,
  `NotificationEmailBuildContext.CultureName` now accepts the empty string, which is the invariant culture.

### Removed

- **Breaking.** Removed `NullEmailNotificationAddressResolver` — an empty resolver chain already resolves no
  address.

## [10.0.0-preview.1] - 2026-07-09

### Added

- Initial public release: an event-driven, pluggable notification framework for ABP Framework
  (`core/`), plus an optional Notification Center providing a persistent inbox, subscriptions,
  read/unread state, and a REST API, with MVC and Angular UI libraries
  (`notification-center/`).
- Dual persistence support (EF Core and MongoDB) behind a shared `INotificationStore` abstraction.
- Real-time push (SignalR) and email notifiers.
- Optional permission gating for notification definitions via ABP Identity.
