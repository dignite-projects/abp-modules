---
name: file-storing-invariants
description: Hard invariants of the Dignite.Abp.FileStoring module — IFileStorer as the single pipeline runner, size limits binding while copying, real-content MIME detection, generated blob names and no-overwrite saves, blob consistency through compensation, container-name validation, DI lifetime discipline, cancellation/tenant/PII, and the anti-scope list. Read BEFORE changing anything under file-storing/ that touches uploads, the handler pipeline, blob writes, MIME detection, or a service's DI lifetime.
---

# Hard Invariants — Read Before Touching Uploads, the Pipeline, Blobs, or DI Lifetimes

> For this module's *conventions* (handler shape, configuration objects, error codes, localization) see the
> `file-storing-conventions` skill. Generic ABP conventions live in the repo-root `abp-*` skills.

## What this module is (and isn't)

`Dignite.Abp.FileStoring` is a thin, correct layer **on top of ABP BlobStoring**: an upload goes through a
per-container `IFileHandler` pipeline and into an ABP blob container, run by `IFileStorer`. It is a **library for
consuming apps**, not a storage product and not a file manager. It keeps **no file metadata, no directories, no HTTP
API and no UI** — the application that stores a file owns its record of it and its authorization (Site's media
library, Campus Support's attachments). File Explorer, the DDD file browser that used to sit on top of this, left in
`10.0.0-rc.25` for the `site` repository; see [`docs/core-only-decision.md`](../../../docs/core-only-decision.md). The
north star: stay a thin, safe layer on ABP BlobStoring and ABP conventions — don't reinvent blob storage, don't grow a
metadata or delivery platform.

## 1. `IFileStorer` is the one pipeline runner — and limits bind while copying, before anything is buffered whole

`FileStorer.StoreAsync` is the only code that executes a container's `TypeList<IFileHandler>`. Its order is the
contract: **validate the container name → copy into a buffer capped at the container's limit → detect the MIME type
from the content → run the handlers in configured order → SHA-256 → generate the blob name → save**. Keep it. Don't
add a second loop over `FileHandlers` somewhere else — a caller that needs the pipeline calls `IFileStorer`.

- **The size limit binds during the copy.** `CopyToBufferAsync` throws `FileTooLarge` as soon as the bytes read pass
  the limit, and rejects a seekable source whose remaining length already exceeds it without reading. A container with
  no `AddFileSizeLimitHandler` still gets `FileConsts.DefaultMaxFileSizeInBytes` — never copy without a cap.
  `FileSizeLimitHandler` stays as a post-transform check; it is not the primary guard. Hosts still enforce a request
  body limit at the HTTP layer.
- **Why a buffer at all**: the handlers (`ImageResizeHandler` identifies, then decodes) and the signature probe need a
  re-readable stream, and the hash must describe the stored bytes. The buffer is in memory and bounded by the limit;
  limits above ~2 GB are not supported by this design (a spill-to-disk buffer would be the change, not removing the
  cap).
- **Image handlers must bound work before decoding.** `ImageResizeHandler` caps pixel count, width/height and
  compression ratio and time-boxes the decode (linked with `FileHandlerContext.CancellationToken`) *before* fully
  decoding — otherwise a decompression bomb consumes CPU/RAM.
- **Stream ownership**: the caller's stream is read once and never disposed. Every stream created during the store —
  the buffer and whatever a handler assigns to `context.BlobStream` — is the storer's and is disposed at the end. A
  handler that replaces the stream leaves the one it received open.

## 2. Never trust the client-supplied MIME type or extension — detect the real content

`IFileStorer` takes **no MIME type parameter**. `IMimeTypeDetector` (default `MimeTypeDetector`) decides from the
bytes' signature and reconciles it with the extension: a contradiction (an executable named `.png`, text named
`.pdf`, a ZIP named `.pdf`) is rejected with `FileErrorCodes.Files.ContentTypeMismatch`; within one kind the content
wins (a PNG named `.jpg` is `image/png`); formats with no signature (text, CSV, JSON, SVG) fall back to the extension;
an extension whose format always has a signature but whose content has none is rejected. So when
`FileTypeCheckHandler` checks the extension, the extension has already been proven consistent with the content, and
`FileHandlerContext.MimeType` is the detected type. After a handler replaced the stream, the type is detected again.

- Don't weaken a signature check into a 2-byte prefix match that text can satisfy (see `IsBmp` /
  `IsPortableExecutable`): a false positive rejects legitimate text uploads.
- Every type in `ImageFormatHelper.AllowedImageUploadFormats` must stay detectable (tested in
  `FileStorerImaging_Tests`).

## 3. Blob names are generated, and a save never overwrites

- Blob names come from the container's `IBlobNameGenerator` (`SetBlobNameGenerator<T>()`, default
  `RandomBlobNameGenerator` = a GUID). Callers don't choose names.
- `SaveAsync` is called with `overrideExisting: false`. A collision surfaces as `BlobAlreadyExistsException` and is
  **never** compensated by deleting — that blob belongs to someone else.
- **Content dedup is not core's job.** `StoredFileInfo.Hash` (SHA-256, upper-case hex, of the stored bytes) is what a
  metadata layer dedups on, tenant- and container-scoped, with its own unique index as the arbiter. If a caller
  dedups by pointing a new record at an existing blob, *it* must not delete that blob while any record references it.

## 4. Blob and metadata must not drift — ordering and compensation, no outbox

Core's part: when `SaveAsync` fails after it may have written bytes, `FileStorer` deletes that blob on an
**independent, bounded token** (`CompensationTimeout`) — the request token may already be cancelled, which is exactly
when cleanup matters (tested: `StoreAsync_Should_Still_Compensate_When_Cancelled_During_The_Save`). Compensation is
best-effort and never hides the original exception.

The caller's part (document it, don't build it into core): store the blob **first**, then write the metadata row
(with `autoSave` or inside a unit of work it controls), and on a failed write call `IFileStorer.DeleteAsync` for the
returned blob name. An overwrite must not delete the old blob before the replacement is durably stored. **Do not**
"solve" this with an ETO/outbox — that's the wrong scale for an in-request file write.

## 5. `ContainerNameValidator` must actually validate

Both `StoreAsync` and `DeleteAsync` run it first. An unregistered container name (one that resolves to the default
configuration) is rejected with `FileErrorCodes.Containers.NotFound` — don't let unknown containers fall through to
the default container. Authorization (who may store into or delete from a container) is the calling application's;
core has no permissions.

## 6. (moved) Directory-tree integrity

Directories were File Explorer's and moved with it to the `site` repository. Nothing here models them.

## 7. DI lifetime discipline — never let a Singleton capture per-request state

Before marking a service `ISingletonDependency`, check every constructor dependency (transitively) for anything
per-request. `FileStorer`, `MimeTypeDetector`, the handlers and `ContainerNameValidator` are transient; `FileStorer`
resolves handlers in a scope of its own per call. Keep new handlers/services transient unless every dependency is
proven safe to capture.

## 8. Cancellation, tenant scope, PII

- **Flow `CancellationToken`** through the copy, the handlers (`FileHandlerContext.CancellationToken`), image decode
  and blob I/O. The one deliberate exception is the compensating delete in §4.
- **Tenant scope** is ABP BlobStoring's: `IBlobContainer` scopes blobs by the current tenant per the container's
  `IsMultiTenant`. Don't add a parallel tenant mechanism, and don't change tenant inside the storer.
- **Don't log file contents, blob bytes or file names with personal data** outside Development.

## 9. (moved) Update vs patch

Metadata updates were File Explorer's and moved with it. Core stores immutable blobs: to change content, store a new
blob and let the caller repoint and delete.

## 10. Keep it thin — the anti-scope list

These are deliberately kept out; don't add them without a real, in-repo need:

- **No distributed events / outbox / ETOs.** The pipeline is inline; consistency is §4's ordering and compensation.
- **No metadata, no persistence, no HTTP API, no UI in this module.** Descriptors, directories, dedup records,
  controllers, permissions and pickers belong to the application that owns the files (Site's media library, Support's
  attachments) — see [`docs/core-only-decision.md`](../../../docs/core-only-decision.md).
- **No dependency beyond ABP BlobStoring (+ ABP Imaging for `.Imaging`)** — no EF Core, no MongoDB, no ASP.NET Core.
  When this repository moves to ABP ≥ 10.8, stream-only transforms are candidates for ABP's own
  `IBlobPipelineContributor` rather than for new machinery here.
