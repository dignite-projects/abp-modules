# Decision: file-storing is core-only; there is no FileService

> **Superseded, 2026-10-11** by [`pipeline-contributors-decision.md`](pipeline-contributors-decision.md). The decision
> below that File Explorer and the FileService leave this module still stands; what changed is the "core" it keeps.
> ABP's `IBlobPipelineContributor` arrived in ABP **10.7.0** (this text first said 10.8, which was wrong), the
> `IFileHandler` pipeline and `IFileStorer` were replaced by contributors for it, and the packages were renamed
> `Dignite.Abp.BlobStoring.*`. Read the rest as history: its names (`IFileStorer`, `IFileHandler`, `Dignite.Abp.FileStoring`)
> no longer exist.

**Status:** accepted, 2026-10-10 · **Applies from:** `10.0.0-rc.25`

## Decision

`file-storing/` keeps only the enhancement layer on ABP BlobStoring — `Dignite.Abp.FileStoring` (the
`IFileHandler` pipeline, its handlers, `IFileStorer`, `IMimeTypeDetector`) and
`Dignite.Abp.FileStoring.Imaging`. File Explorer (`Dignite.FileExplorer.*`, `@dignite/ng.file-explorer`) and the
flex-fields file-picker field type built on it leave this repository. The file browser becomes a feature of Site, in
the `site` repository, under the Site namespace. No microservice owns "files".

## Why not a FileService

The plan we dropped was to host File Explorer as its own microservice that Site (media library) and Campus Support
(attachments) both call.

1. **A module with an HTTP API cannot be shared by two services behind one gateway.** File Explorer brings
   conventional controllers under one fixed route prefix (`/api/file-explorer/...`) and one permission group. Installed
   in both Site and Support, the gateway sees the same routes from two services and can route them to only one; the
   permission names collide in the shared permission management as well. Hosting it once, as a service, is what
   prompted the FileService idea — but that only moves the problem (see the rejected options below).
2. **The file browser is a product feature of Site, not platform infrastructure.** Directories, a media picker,
   image presets and the "which page uses this image" questions are CMS concerns. ABP's own precedent is CmsKit: its
   media descriptors live inside CmsKit, not in a shared file service. Site follows that.
3. **What the platform genuinely shares is blob storage.** ABP BlobStoring already is the shared layer: every service
   configures its containers against the same provider. What was missing was a safe way to put bytes into a container
   (size cap while streaming, content-based MIME, handler pipeline, compensation) — that is `IFileStorer`, and it is a
   library, not a service.
4. **Each service owns its file metadata and its authorization.** A Support attachment is authorized by the ticket it
   belongs to; a Site media item by Site's permissions. A shared FileService would either have to learn every owner's
   rules or ask them back over HTTP for each file — coupling the services through the very component meant to be
   neutral.

## Rejected options

| Option | Why it was rejected |
|---|---|
| **Gateways split by product** (each product's gateway routes `/api/file-explorer` to its own copy of the module) | Works only while no client talks to two products through one gateway; the permission-name and route collisions remain inside any shared admin UI, and two copies of the same tables drift. It hides the conflict instead of removing it. |
| **FileService + tickets** (the service stores bytes; the owning service issues a signed ticket per upload/download) | Every upload and download becomes a two-service round trip, the ticket format becomes a cross-service contract, and the owning service still keeps its own metadata — so the FileService adds a hop without owning anything. |
| **FileService + a definition store** (owners register containers and permission rules in the FileService) | The FileService would have to evaluate other services' authorization (e.g. "may this user see this ticket's attachment") from definitions alone, which it cannot; it ends up calling back to the owner anyway. |

## Where core sits relative to ABP 10.7.0 `IBlobPipelineContributor`

ABP 10.7.0 adds `IBlobPipelineContributor` (`BlobContainerConfiguration.PipelineContributors`): per-container
contributors that transform the content stream inside `IBlobContainer.SaveAsync`/`GetAsync`. It has the same shape as
our `IFileHandler` (per-container ordered type list, a context with the stream to replace), and that confirms the
direction: content processing belongs to the container, not to a metadata service.

They are not interchangeable yet, and this repository stays on ABP 10.5:

- `IBlobPipelineContributor` runs inside every save of any blob, with only the blob name; it has no file name to
  check an extension against and no place to report a detected MIME type, size or hash back to the caller.
  `IFileStorer` is an upload entry point that returns `StoredFileInfo` for the caller's metadata row.
- Our handlers validate *uploads* (size, type, image bounds); a contributor also runs for system writes that never were
  uploads.

When the platform moves to ABP ≥ 10.7.0, the stream-only transforms (`ImageResizeHandler`'s resize/compress) can be
re-expressed as pipeline contributors, and `IFileStorer` shrinks to "cap, detect, hand to the container, report".
Until then core keeps `IFileHandler` and does not depend on 10.7.0.

## Consequences

- Site owns the media library (directories, descriptors, dedup by `StoredFileInfo.Hash`, its API and UI) and calls
  `IFileStorer` to store bytes.
- Campus Support stores attachments with `IFileStorer` and keeps them on its own ticket entities.
- Invariants that were about descriptors (tenant-scoped blob-name uniqueness, reference dedup, directory cycles,
  resource authorization) move with File Explorer to Site; this module's invariants are the pipeline ones.
