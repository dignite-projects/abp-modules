---
name: blob-storing-conventions
description: How the Dignite.Abp.BlobStoring modules (Pipeline and Imaging) apply ABP — IBlobPipelineContributor implementations with a per-container *ContributorConfiguration and an Add…Contributor extension, BlobStreamBuffering, IMimeTypeDetector as a replaceable service, DI lifetimes, the two error-code namespaces and localization, test conventions on Volo.Abp.BlobStoring.Memory. Read when writing or reviewing code under blob-storing/ and the generic abp-* skill doesn't say what THIS module does.
---

# blob-storing — Module Conventions

> Where this file and a generic `abp-*` skill disagree, **this file wins for code under `blob-storing/`**.
> The hard rules (stream ownership, caps, content-decides-type, stored formats) are in the `blob-storing-invariants`
> skill.

## What is here

Two packages, no DDD layers: `Dignite.Abp.BlobStoring.Pipeline` (depends on `Volo.Abp.BlobStoring` and FileSignatures)
and `Dignite.Abp.BlobStoring.Imaging` (the Pipeline package + `Volo.Abp.Imaging.Abstractions`, no image library). No
entities, repositories, application services, controllers, permissions, settings or features — and none should be added
(invariants §8). The naming follows ABP: `Dignite.Abp.BlobStoring.<Addition>` mirrors `Volo.Abp.BlobStoring.<Provider>`.

## Contributors

A pipeline step is an `IBlobPipelineContributor`, a **plain class** registered as `ITransientDependency`. It inherits no
ABP base class, so there is no inherited `Clock`/`CurrentUser`/`L` — inject what it needs:

```csharp
public class MyContributor : IBlobPipelineContributor, ITransientDependency
{
    public virtual async Task OnSavingAsync(BlobPipelineContext context)
    {
        var configuration = context.Configuration.GetMyContributorConfiguration();
        configuration.Validate();
        // validate: probe a seekable stream in place and rewind, or buffer with BlobStreamBuffering; throw BusinessException
        // transform: assign a new stream to context.BlobStream; leave the received one open
        // flow context.CancellationToken into every I/O call
    }

    public virtual Task OnGettingAsync(BlobPipelineContext context) => Task.CompletedTask;   // or undo the transform
}
```

Every public and protected member is `virtual` (ABP module rule); a contributor's steps are `protected virtual` so an
application can subclass and replace it with `[Dependency(ReplaceServices = true)]`.

Each contributor has three companions, following `MaxSizeContributor`:

- `MyContributorConfiguration` — a wrapper over `BlobContainerConfiguration` whose properties read and write named
  entries (`GetConfigurationOrDefault` / `SetConfiguration`) and a `Validate()` that throws `AbpException` on a setup that
  cannot work. Values are validated in `Validate()`, not in the setters, because the configuration is read back from the
  container on every save.
- The entry names as constants in `BlobStoringPipelineConfigurationNames` / `BlobStoringImagingConfigurationNames`, under
  the package's prefix.
- `AddMyContributor(this BlobContainerConfiguration, Action<MyContributorConfiguration>)` in
  `BlobContainerConfigurationExtensions` (Pipeline) or `ImagingBlobContainerConfigurationExtensions` (Imaging; named
  apart so a file importing both namespaces can name either). It runs the configure action, calls `Validate()`, and
  `TryAdd<MyContributor>()`s to `PipelineContributors`, so the configured order is the execution order and a contributor
  is added once; calling it again only updates the configuration. A contributor whose configuration is optional takes an
  `Action<…>?`. A `GetMyContributorConfiguration()` reader sits beside it.

Per-container limits are container configuration, **not** ABP settings.

An image contributor derives from `ImageContributorBase` and implements `ValidateConfiguration` and `ProcessAsync`; the
base owns the seekable copy, the type check, the decode guard, the timeout and the result mapping.

## Services

| Service | Default | Lifetime | Replace with |
|---|---|---|---|
| `IMimeTypeDetector` | `MimeTypeDetector` | transient | `[Dependency(ReplaceServices = true)]` subclass; extend `DetectFromSignature` / `DetectFromContent` / `DetectTextFormat` / `GetMimeTypeFromExtension` / `GetKind` |
| `IFileFormatInspector` (FileSignatures) | `FileFormatInspector` over `FileFormatLocator.GetFormats()` | singleton, `TryAdd` | register your own with more `FileFormat` types |
| the contributors | themselves | transient | subclass + `[Dependency(ReplaceServices = true)]` |

`BlobStreamBuffering` is a static helper, not a service.

DI lifetime: the contributors, the detector and the configuration wrappers are transient or plain objects; the one
singleton is the signature inspector, which is stateless once built. Check every constructor dependency (transitively)
before marking anything `ISingletonDependency`.

## Error codes and localization

Codes live in `BlobStoringPipelineErrorCodes` (`Dignite.Abp.BlobStoring:…`, mapped to `BlobStoringPipelineResource` in
`DigniteAbpBlobStoringPipelineModule`) and `BlobStoringImagingErrorCodes` (`Dignite.Abp.BlobStoring.Imaging:…`, mapped to
`BlobStoringImagingResource` in `DigniteAbpBlobStoringImagingModule`). ABP maps a code namespace to exactly one resource,
which is why the Imaging package has a namespace of its own. Codes are named (`ContentTooLarge`, `ImageTooSmall`), not
numbered. Every code needs a key in **all four** resource files
(`Localization/Resources/{en,ja,zh-Hans,zh-Hant}.json`); data placeholders use `{Name}` and are filled with
`.WithData("Name", value)`. The embedded resources are registered through `AddEmbedded<TModule>()` and
`AddVirtualJson(...)` in the module class.

Invalid configuration is an `AbpException` (a programming error), not a `BusinessException` (a bad input).

## Tests

Integration tests on `AbpIntegratedTest<…TestModule>` with `Volo.Abp.BlobStoring.Memory`. The test module replaces
`MemoryBlobProvider` with one shared singleton so a test can read what a container stored straight from the provider
(`GetStoredBytesAsync`), bypassing the pipeline — which is how a test proves that a rejected save stored nothing or that a
transform changed the stored bytes. Containers are registered by name in the test module (`MaxSizeContainer`,
`ComposedContainer`, …) and exercised through `IBlobContainer`. Pipeline tests use stream doubles that count the bytes read
and hide seeking (`TrackingStream`); Imaging tests run on SkiaSharp for real encodes and on scripted fakes of
`IImageResizer` / `IImageCompressor` for results and failures a real provider cannot produce on demand. Shouldly + xUnit, as
in the repo-root `abp-testing` skill; don't add a real storage provider to the test projects.

Error codes get a theory that localizes each code per culture (`Localization_Tests`).

## Anti-patterns specific to this module

| Don't | Do instead |
|---|---|
| Accept a MIME type from the caller, or decide from the extension alone | `IMimeTypeDetector` on the content — invariants §4 |
| Copy content into memory without a cap | `BlobStreamBuffering` with the container's limit or `DefaultMaxBufferedBytes` — invariants §3 |
| Read the stream in a validator and return without replacing it | Probe in place and rewind, or buffer and assign the copy — invariants §2 |
| Reference an image library from `.Imaging` | `IImageResizer` / `IImageCompressor`; the application picks the provider — invariants §6 |
| Add an upload service, a return object, blob naming, metadata, an API or a UI | Leave them to the application — invariants §8 |
| Add a transforming contributor to a container that already has blobs without a plan | State the stored-format consequence — invariants §5 |
