---
name: blob-storing-invariants
description: Hard invariants of the Dignite.Abp.BlobStoring modules — ABP's stream-ownership contract for IBlobPipelineContributor, validating contributors never passing an unvalidated stream through, every buffering path capped, the content (never the uploader's claim) deciding the type, one-way transforms versus stored-format transforms and what each means for existing blobs, the image decode guard and no image library in Imaging, the anti-scope list, localized error codes and cancellation tokens on all I/O. Read BEFORE changing anything under blob-storing/ that touches a contributor's stream handling, buffering, MIME detection, an image contributor, or whether a contributor changes the stored format.
---

# Hard Invariants — Read Before Touching Streams, Buffering, MIME Detection, Images, or Stored Formats

> For this module's *conventions* (contributor shape, configuration objects, extension methods, error codes,
> localization, tests) see the `blob-storing-conventions` skill. Generic ABP conventions live in the repo-root `abp-*`
> skills.

## What this module is (and isn't)

`Dignite.Abp.BlobStoring.Pipeline` and `.Imaging` are **everyday `IBlobPipelineContributor` implementations for ABP
BlobStoring**: ABP ships the pipeline mechanism, this ships contributors for it. A contributor runs inside
`IBlobContainer.SaveAsync` / `GetAsync` for every blob of a container that lists it — that is the whole product. It
keeps **no file metadata, has no upload entry point, no return object, no blob naming, no HTTP API and no UI**; the
application that stores a file owns its record of it, its authorization and its blob names. The old `IFileStorer` /
`IFileHandler` pipeline this replaced is gone on purpose: [`docs/pipeline-contributors-decision.md`](../../../docs/pipeline-contributors-decision.md).

## 1. Stream ownership follows ABP's contract — exactly

- A replacement **leaves the stream it received open**. Every stream *assigned* to `BlobPipelineContext.BlobStream` is
  disposed by the pipeline after the save; the original belongs to the caller.
- A stream is tracked **only from the moment it is assigned**. A contributor that creates a stream and then does work that
  can fail before assigning it disposes it itself on the failure path (see `MaxSizeContributor` / `GZipContributor` /
  `BlobStreamBuffering.CopyToBufferAsync`).
- While reading (`OnGettingAsync`), a replacement **disposes the stream it received when it is disposed** — the composed
  stream goes back to the caller as a whole (`GZipContributor` wraps in a `GZipStream` that owns it).
- Never dispose `context.BlobStream` you did not create, and never assign a stream that an earlier step still reads.
- Contributors run in configuration order on save and in reverse on get; ABP's built-in encryption runs after them on save.

## 2. A validating contributor never lets an unvalidated stream through

A contributor that reads the content to validate it either **probes a seekable stream in place and rewinds it**, or
**replaces it** with a pass-through wrapper that validates as bytes flow, or an eagerly materialized, capped copy.
Reading the content and returning without doing one of these leaves an empty or truncated stream for the provider. Not
replacing the stream is valid only for a contributor that does not consume the content.

- `AllowedContentTypesContributor` and the image contributors get their stream from
  `BlobStreamBuffering.EnsureSeekableAsync`, which returns the stream itself when it is seekable and at position 0 and
  otherwise assigns a capped copy. The detector restores position 0; keep it that way.
- Never "validate" from the claimed content type or the file name alone.

## 3. Every buffering path is capped

Content is never copied into memory without a bound. The bound is the container's `MaxSizeContributor` limit, or
`BlobStoringPipelineConsts.DefaultMaxBufferedBytes` (100 MB) when it has none. `MaxSizeContributor` enforces its limit
**while copying** (it rejects a seekable stream whose remaining length already exceeds it without reading, and any stream
as soon as the copy passes it) — never check size after buffering the whole thing. Limits above `Array.MaxLength` are not
supported by this design (a spill-to-disk buffer would be the change, not removing the cap).

- A new contributor that has to re-read the content buffers through `BlobStreamBuffering`, with the same limit.
- `MaxSizeContributor` goes first in a container: it materializes the seekable, length-aware copy everything after it reuses.
- **Known gap:** `GZipContributor` compresses the whole input into memory and has no cap of its own; a container is only
  bounded if `MaxSize` runs before it. Don't copy that shape into a new contributor, and don't add GZip to a container
  that takes untrusted uploads without `MaxSize` in front of it.
- Hosts still enforce a request-body limit at the HTTP layer.

## 4. The content decides the type — never the uploader's claim

`IMimeTypeDetector` decides from the bytes' signature and reconciles the result with the extension of the blob name: a
contradiction (an executable named `.png`, text named `.pdf`, a ZIP named `.pdf`) fails with
`BlobStoringPipelineErrorCodes.ContentTypeMismatch`; within one kind the content wins (a PNG named `.jpg` is `image/png`);
formats with no guaranteed signature (text, CSV, JSON, SVG, MP4…) fall back to the extension; an extension whose format
always carries a signature, with content that has none, is rejected. Unknown content is `application/octet-stream`.

- **No MIME type parameter anywhere.** The contributors take none from a caller; the detector's `fileName` is optional and
  only its extension matters.
- Don't weaken a signature check into a 2-byte prefix match that text can satisfy (see `IsBmp` / `IsPortableExecutable` /
  `IsId3Tag`): a false positive rejects legitimate text uploads. Short signatures must be confirmed by the rest of the header.
- The detector must stay safe on hostile input: a malformed archive must not become a server error (the inspector's
  exceptions are caught, and an oversized `[Content_Types].xml` is not opened).
- Magic bytes are necessary, not sufficient (polyglots, hostile-but-allowed types). Say so in documentation; don't imply
  the detector makes an upload safe to serve.

## 5. One-way transforms and stored-format transforms are different, and say so

- **One-way** (`ImageResize`, `ImageCompress`, `MaxSize`, `AllowedContentTypes`): nothing is undone while reading, so a
  contributor can be added to or removed from a container that already has blobs; existing blobs read as stored.
- **Stored-format** (`GZip`): the transform is undone while reading, so a blob can only be read with the same transforming
  contributors, in the same order, that saved it. Adding or removing one on a container that has blobs breaks every
  existing blob. A new contributor that undoes something in `OnGettingAsync` is stored-format: document it in its XML doc,
  in the README and in `Add…Contributor`'s doc.
- Order is part of a container's stored format: contributors added later come later on save and earlier on get.
- The recommended order is validation (`MaxSize`, `AllowedContentTypes`) → image → GZip. Anything that must inspect the
  content runs before a stored-format transform makes it opaque.

## 6. Image contributors: guard before decode, no image library

- `ImageContributorBase` runs the **decode guard before anything decodes the image**: dimensions read from the header by
  `ImageHeaderReader` (managed, no decode) are checked against `ImageDecodeGuardConfiguration` (width, height, pixels,
  pixels per stored byte), and the provider call runs under the guard's `DecodeTimeout`, linked with the operation's token.
  Don't add a path that hands an image to `IImageResizer` / `IImageCompressor` without it.
- The timeout is cooperative: it only interrupts a provider that observes the token while decoding. Don't promise more in
  documentation; the dimension guard and the byte cap are the protections for providers that decode synchronously.
- `Dignite.Abp.BlobStoring.Imaging` references **`Volo.Abp.Imaging.Abstractions` only**. The application picks a provider
  (SkiaSharp, ImageSharp or Magick.NET). Never reference a provider or an image library from the package; only the test
  project does.
- Content that is not `image/*`, and an image the provider reports `Unsupported`, passes through unchanged. With known dimensions a
  resize never upscales and leaves an image that already fits byte for byte.
- Defaults changes (the guard's numbers) are behaviour changes: record them in the CHANGELOG.

## 7. Error codes, localization, cancellation

- Every error code has a key in **all four** resource files (`en`, `ja`, `zh-Hans`, `zh-Hant`) and is in the localization
  theory. The two code namespaces (`Dignite.Abp.BlobStoring`, `Dignite.Abp.BlobStoring.Imaging`) each map to their own
  resource; never reuse a namespace across resources.
- **Pass the cancellation token to all I/O**: `context.CancellationToken` into the copy, the detector, the compressor, the
  provider call. The decode timeout is linked to it so a caller's cancellation surfaces as `OperationCanceledException`,
  never as the timeout error.
- **Tenant scope** is ABP BlobStoring's (`BlobPipelineContext.TenantId`). Don't add a parallel tenant mechanism.
- **Don't log file contents, blob bytes or file names with personal data** outside Development.
- Contributors, the detector and the configuration wrappers are transient or plain; don't make one a singleton unless every
  constructor dependency is proven safe to capture.

## 8. Keep it thin — the anti-scope list

Deliberately kept out; don't add them without a real, in-repo need and a new decision document:

- **No upload entry point, no return object, no blob naming** — no `IFileStorer`, no `StoredFileInfo`, no
  `IBlobNameGenerator`. Applications call `IBlobContainer`; one that wants the detected MIME type or a hash calls
  `IMimeTypeDetector` and hashes itself; it names the blob, saves with `overrideExisting: false`, and deletes the blob if
  its metadata write fails. Option B (a thin wrapper in front of the container) was weighed and rejected.
- **No metadata, persistence, HTTP API, UI or permissions.** They belong to the application that owns the files.
- **No distributed events, outbox, ETOs or audit events.** The pipeline is inline.
- **No contributor that is not a one-to-one stream transform or check** (no thumbnails, dedup, hash sidecars).
- **No dependency beyond ABP BlobStoring** (+ FileSignatures for detection, + ABP Imaging abstractions for `.Imaging`) —
  no EF Core, no MongoDB, no ASP.NET Core, no image library.
- **No cross-module reference**: this module never references `notifications/`, `flex-fields/` or anything else in the
  repository (root `CLAUDE.md`, invariant 2).
