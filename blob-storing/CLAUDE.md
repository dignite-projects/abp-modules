# Dignite.Abp.FileStoring

An enhancement layer on **ABP BlobStoring** (LGPL-3.0-only): a per-container `IFileHandler` upload pipeline
and `IFileStorer`, the one service that runs it and puts the result into a blob container. Published:
`core/src/` (`Dignite.Abp.FileStoring` + `.Imaging`). Nothing else lives here — no DDD layers, no
persisted metadata, no HTTP API, no UI.

File Explorer (`Dignite.FileExplorer.*`, `@dignite/ng.file-explorer`) used to live here and left in
`10.0.0-rc.25`: the file browser is Site's media library and now lives in the `site` repository. Why, and
what was rejected: [`docs/core-only-decision.md`](./docs/core-only-decision.md). Don't add a metadata
layer, an API or a UI back here — an application that needs them owns them and calls `IFileStorer`.

## Structure

One `.slnx` — `Dignite.Abp.FileStoring.slnx`:

- **`core/src/Dignite.Abp.FileStoring`** — `IFileHandler` + `FileHandlerContext`, the handlers
  (`FileSizeLimitHandler`, `FileTypeCheckHandler`), their `*Configuration` / `*ConfigurationNames` and
  `Add…Handler` extensions, `IFileStorer`/`FileStorer` + `StoredFileInfo`, `IMimeTypeDetector`/
  `MimeTypeDetector`, `ContainerNameValidator`, `IBlobNameGenerator`/`RandomBlobNameGenerator`
  (`SetBlobNameGenerator<T>()`), `ImageFormatHelper`, `FileConsts`/`FileErrorCodes`/localization.
  Depends only on `Volo.Abp.BlobStoring`.
- **`core/src/Dignite.Abp.FileStoring.Imaging`** — `ImageResizeHandler` (`AddImageResizeHandler`), on
  ABP Imaging + ImageSharp.
- **`core/test/`** — `Dignite.Abp.FileStoring.Tests` (handlers, `FileStorer`, `MimeTypeDetector`, with an
  in-memory fake blob provider) and `Dignite.Abp.FileStoring.Imaging.Tests`.

Namespace-mirrored files: `<Project>/<namespace path>/File.cs`, `<RootNamespace/>` empty.

## The pipeline

```csharp
options.Containers.Configure<MyPicturesContainer>(c =>
{
    c.AddFileSizeLimitHandler(h => h.MaxFileSize = 2);          // MB
    c.AddFileTypeCheckHandler(h => h.AllowedFileTypeNames = [".png", ".jpg"]);
    c.AddImageResizeHandler(h => { h.ImageWidth = 1024; h.ImageHeight = 1024; });
});

StoredFileInfo stored = await fileStorer.StoreAsync<MyPicturesContainer>(fileName, stream, cancellationToken);
// stored.BlobName / Size / MimeType / Hash (SHA-256) describe the bytes actually stored
```

`FileStorer.StoreAsync`, in order: `ContainerNameValidator` → copy into a buffer capped at the container
limit (`FileConsts.DefaultMaxFileSizeInBytes` if none) → `IMimeTypeDetector` on the content → the
container's handlers in their configured order → SHA-256 → `IBlobNameGenerator` → `SaveAsync` without
overwrite, deleting the blob again if the save fails. Validators inspect `context.BlobStream`; transforms
replace it. New upload rules = new handlers.

Content dedup is **not** core's: a caller that wants it compares `StoredFileInfo.Hash` against its own
metadata. A caller that writes metadata writes it after `StoreAsync` and calls `DeleteAsync` if that
write fails.

Read the `file-storing-invariants` skill before touching any of this.

## Adding a feature

**New upload rule/transform**:
1. `IFileHandler` impl in `Dignite.Abp.FileStoring` (or `.Imaging`), `ITransientDependency`. Use
   `context.CancellationToken` for any I/O or decoding.
2. `*Configuration` + `*ConfigurationNames`, and an `Add…Handler(...)` extension that `TryAdd<>`s it under
   `BlobContainerConfigurationNames.FileHandlers`.

**New format for MIME detection**: extend `MimeTypeDetector` (its protected virtual members) and replace
it with `[Dependency(ReplaceServices = true)]`, or add the signature to the default when it is generally
useful.

## Commands

```bash
dotnet build Dignite.Abp.FileStoring.slnx
dotnet test Dignite.Abp.FileStoring.slnx
dotnet pack Dignite.Abp.FileStoring.slnx -c Release
```
