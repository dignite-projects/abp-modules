---
name: file-storing-conventions
description: How the Dignite.Abp.FileStoring module applies ABP — IFileHandler implementations and their per-container configuration objects, the Add…Handler extension pattern, IFileStorer/IMimeTypeDetector as replaceable services, DI lifetimes, error codes and localization. Read when writing or reviewing code under file-storing/ and the generic abp-* skill doesn't say what THIS module does.
---

# file-storing — Module Conventions

> Where this file and a generic `abp-*` skill disagree, **this file wins for code under `file-storing/`**.
> The hard rules (pipeline order, caps, MIME, compensation) are in the `file-storing-invariants` skill.

## What is here

Two packages, no DDD layers: `Dignite.Abp.FileStoring` (depends only on `Volo.Abp.BlobStoring`) and
`Dignite.Abp.FileStoring.Imaging` (+ `Volo.Abp.Imaging.*`). No entities, repositories, application services,
controllers, permissions, settings or features — and none should be added (invariants §10). File metadata and its
authorization belong to the consuming application.

## Handlers

An upload rule or transform is an `IFileHandler`, a **plain class** registered as `ITransientDependency`. Handlers
inherit no ABP base class, so there is no inherited `Clock`/`CurrentUser`/`L` — inject what you need:

```csharp
public class MyHandler : IFileHandler, ITransientDependency
{
    public async Task ExecuteAsync(FileHandlerContext context)
    {
        var configuration = context.ContainerConfiguration.GetMyHandlerConfiguration();
        // validate: read context.BlobStream (positioned at 0), throw BusinessException on violation
        // transform: assign a new stream to context.BlobStream; leave the received one open
        // flow context.CancellationToken into any I/O or decoding
    }
}
```

Each handler has three companions, following `FileSizeLimitHandler`:

- `MyHandlerConfiguration` — a wrapper over `BlobContainerConfiguration` whose properties read/write named entries
  (`GetConfigurationOrDefault` / `SetConfiguration`) and validate values in the setter.
- `MyHandlerConfigurationNames` — the entry names as constants.
- `BlobContainerConfigurationExtensions.AddMyHandler(this BlobContainerConfiguration, Action<MyHandlerConfiguration>)`
  — `TryAdd<MyHandler>()`s into the `BlobContainerConfigurationNames.FileHandlers` `TypeList<IFileHandler>` (so the
  configured order is the execution order and a handler is added once), then runs the configure action; plus a
  `GetMyHandlerConfiguration()` reader.

Per-container limits are container configuration, **not** ABP settings.

## Services

| Service | Default | Lifetime | Replace with |
|---|---|---|---|
| `IFileStorer` | `FileStorer` | transient | `[Dependency(ReplaceServices = true)]`; its steps are `protected virtual` |
| `IMimeTypeDetector` | `MimeTypeDetector` | transient | same; extend `DetectFromContent` / `GetKind` / `GetMimeTypeFromExtension` |
| `IBlobNameGenerator` | `RandomBlobNameGenerator` | transient | per container: `SetBlobNameGenerator<T>()` |
| `ContainerNameValidator` | — | transient | subclass + replace |

All public and protected members of these are `virtual` (ABP module rule). Typed-container overloads are extension
methods (`FileStorerExtensions`), the way ABP does `BlobContainerFactoryExtensions.Create<T>()`.

## Error codes and localization

Codes live in `FileErrorCodes` (`Dignite.Abp.File:000n`) and `FileStoringImagingErrorCodes`
(`Dignite.Abp.FileStoring.Imaging:000n`). The `Dignite.Abp.File` namespace is mapped to `FileStoringResource`
(`MapCodeNamespace` in `DigniteAbpFileStoringModule`); every code needs a key in **all four** resource files
(`Localization/Resources/{en,ja,zh-Hans,zh-Hant}.json`). Data placeholders use `{Name}` and are filled with
`.WithData("Name", value)`.

## Tests

Core tests use `AbpIntegratedTest<FileStoringTestModule>` with an in-memory `FakeBlobProvider` (records deletes, can
fail or block after writing) — don't add a real provider dependency to the test projects. Shouldly + xUnit, as in the
repo-root `abp-testing` skill.

## Anti-patterns specific to this module

| Don't | Do instead |
|---|---|
| Accept a MIME type from the caller, or decide from the extension alone | `IMimeTypeDetector` on the content — invariants §2 |
| Copy an upload without a cap, or check size only after buffering | `FileStorer.CopyToBufferAsync`'s capped copy — invariants §1 |
| Loop over `FileHandlers` outside `FileStorer` | Call `IFileStorer` |
| Add descriptors/directories/an API/a UI here | Keep them in the consuming application — invariants §10 |
| Add an ETO/outbox to make metadata+blob writes atomic | Store first, write the row, `DeleteAsync` on failure — invariants §4 |
