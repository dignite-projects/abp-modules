# Hard Invariants — Read Before Touching Uploads, Blobs, Directories, Authorization, or DI Lifetimes

> This file has **no `paths:` frontmatter, so it always loads**, alongside `abp-core.md`, `versioning.md`, and
> `template/app.md`. It encodes the rules the module must uphold: the ones the hardening fixes (issues #45–#70)
> already established as "don't regress," plus the guarantees the audit remediation
> (the closed `audit`-labeled issues #2–#35) drove toward. Where an invariant is **not yet fully enforced**, it says so —
> treat that as "the intended contract; don't make it worse, and close it when you touch that code."

## What this module is (and isn't)

`Dignite.Abp.FileStoring` is a thin, correct layer **on top of ABP BlobStoring**: files are uploaded through a
per-container `IFileHandler` pipeline into ABP's blob containers, and `file-explorer` is a DDD backend
(directories + persisted `FileDescriptor` metadata + REST API) over that. It is a **library for consuming apps**,
not a standalone storage product. The north star is: stay a thin, safe layer on ABP BlobStoring and ABP
conventions — don't reinvent blob storage, don't grow a distributed delivery/outbox platform, don't hand-roll
what ABP already gives you (mapping → Mapperly, UoW → ABP, auditing → ABP audit interfaces). This repo was
**extracted from `dignite-abp`** (treated as a frozen source); it is at `10.0.0-rc.1` with a known backlog, so
correctness of the pipeline, the blob/DB relationship, and authorization matters more than new surface area.

## 1. The handler pipeline runs on the stream *before* the blob is stored — and limits must bind before buffering

An `IFileHandler` sees only its `FileHandlerContext` (`FileName`, `MimeType`, the mutable `BlobStream`, the
container `BlobContainerConfiguration`). `FileDescriptorManager` resolves the container's ordered
`TypeList<IFileHandler>` and runs each `ExecuteAsync` over the upload stream **before** the blob is written —
validators (`FileSizeLimitHandler`, `FileTypeCheckHandler`) inspect, transforms (`ImageResizeHandler`) replace
`context.BlobStream`. Keep this ordering.

- **A size/quota limit must bind before the whole stream is buffered into memory.** Copying a remote stream
  fully into memory and *then* running `FileSizeLimitHandler` means a large request has already allocated at
  scale before rejection. Enforce request-body size at the HTTP layer **and** cap while streaming — don't rely on
  a post-buffer check. (Audit P1-3; the limit unit/wording must also be consistent — KB vs MB confusion was
  flagged.)
- **Image handlers must bound work before decoding.** `ImageResizeHandler` must cap max pixel count, max
  width/height, and compression ratio, and time-box the decode, *before* fully decoding an attacker-supplied
  image — otherwise a decompression bomb consumes CPU/RAM (audit P1-4). The same bounds apply to any on-the-fly
  resize endpoint (which should also cache; see `infrastructure.md`).
- **Why**: these are the paths where an unauthenticated or low-privilege caller can turn a single request into a
  denial of service. The handler seam exists precisely so these checks live in one place — keep them there.

## 2. Never trust the client-supplied MIME type or extension for a security decision — detect the real content

`FileTypeCheckHandler` and the image handlers must decide from the **actual bytes**, not the request's
`Content-Type` or file extension. A caller can send `image/png` for an executable; the on-wire MIME is a hint for
convenience, never the basis for "is this allowed / is this an image." Sniff the real format
(`ImageFormatHelper` / a content probe) before accepting or transforming. (Audit P1-4 content forgery.)

## 3. Blob-name uniqueness and content identity are `(tenant, container)`-scoped, and dedup must be concurrency-safe

- **Unique blob name per `(TenantId, ContainerName, BlobName)` is a hard invariant**, backed by a unique index
  (issue #48). Never generate or accept a blob name without going through `IBlobNameGenerator` and the uniqueness
  guarantee; never write a "global" name check that ignores tenant/container.
- **MD5/content dedup is `(TenantId, ContainerName, Md5)` filtered-unique** (empty MD5 rows excluded). A naive
  "query `Md5ExistsAsync` then insert" is **not** concurrency-safe — two parallel uploads both see "absent" and
  both insert. The database unique constraint is the arbiter; handle its violation rather than trusting the
  pre-check. The audit recommends **SHA-256** over MD5 for collision resistance — prefer it for new work.
- **Reference-based dedup (`ReferBlobName`)** lets a new `FileDescriptor` point at an existing physical blob
  instead of re-storing it. Therefore **a blob may not be physically deleted while any descriptor references
  it** — check `ReferencingAnyAsync` before deleting bytes, or you orphan the referrers.
- **Why**: uniqueness and dedup are the load-bearing correctness properties of a blob store; getting them
  tenant-scoped and race-safe is what keeps one tenant from reading/colliding with another's files.

## 4. Blob and database must not drift — order writes and compensate on failure

Writing the `FileDescriptor` row and writing the blob bytes are **not** one atomic transaction (there is no
distributed outbox here, by design — see `infrastructure.md`). So the manager owns consistency through ordering
and compensation:

- A failed DB commit must not leave an **orphan blob** (bytes with no descriptor).
- An **overwrite** must not delete the old blob before the replacement is durably stored — a mid-way failure
  would lose the original.
- A **delete** must remove bytes and row together, guarded by the reference check in §3.

Pick a deterministic order, compensate on each failure branch, and (audit recommendation) back it with a
background orphan-cleanup sweep rather than assuming every path is failure-free. (Audit P1-5.) **Do not** "solve"
this by adding an ETO/outbox — that's the wrong scale for an in-request file write; fix the ordering.

## 5. Authorization: no bypass via temporary ownership; authorize every resource; the container decides the permission

The authorization model is in `framework/common/authorization.md`; the invariants on top of it:

- **The create-permission check must run before the caller becomes the resource's owner.** Constructing a
  `FileDescriptor` with `CreatorId = current user` and *then* authorizing lets the "creator can act on their own
  resource" branch satisfy the check for free — the configured `CreateFilePermissionName` becomes a no-op. Order
  the check before ownership is established. (Audit P1-1.)
- **Batch operations authorize each resource.** `DeleteByEntityIdAsync`-style paths must run the resource-based
  check per file, not just verify `FileExplorerPermissions.Files.Management` once.
- **Container config is the source of truth** for which permission gates each operation
  (`BlobContainerAuthorizationConfiguration`), plus the optional per-associated-entity handler
  `IFileDescriptorEntityAuthorizationHandler`. Gate through those, not ad-hoc `[Authorize]` on internal paths.
- **`ContainerNameValidator` must actually validate.** An empty/no-op validator combined with permissive
  defaults means an **unregistered** container can be reached by anyone who can guess a blob name. Validate
  container names; don't let unknown containers resolve to "allow." (Audit P1-6.)

## 6. Directory-tree integrity: no self/descendant cycles, validate the parent, block non-empty deletion

`DirectoryManager` owns these — never mutate `DirectoryDescriptor.ParentId` directly to route around them
(the aggregate's public `ParentId` setter is a known gap; see `ddd-patterns.md`):

- **A directory may not move into itself or any descendant.** A cycle makes the recursive tree walk
  (`DirectoryListExtensions`) recurse until it `StackOverflow`s and takes down the process — this is a P0. Moves
  must reject self/descendant targets (`MoveAsync_ShouldRejectMovingDirectoryInto{Itself,Descendant}`).
- **Validate the parent before create/move**: it must exist and share tenant, owner, and container (issue #46,
  `CreateAsync_ShouldReject…`).
- **Non-empty directories can't be silently deleted** — require an empty check or an explicit cascade policy
  (issue #47, `EnsureEmptyAsync_ShouldRejectDirectoriesWithFiles`).

## 7. DI lifetime discipline — never let a Singleton capture per-request state

Before marking a service `ISingletonDependency`, check every constructor dependency (transitively) for anything
backed by a repository, `DbContext`, or other per-request/per-unit-of-work state (the custom repositories are
exactly this). If it's request-scoped, the service must be `ITransientDependency` (or resolve the scoped
dependency from `IServiceProvider` on demand). Autofac won't fail this at startup; it fails under concurrent load
with thread-unsafe `DbContext` use. The `IFileHandler` implementations are correctly transient — keep new
handlers/managers transient unless you've proven every dependency is safe to capture.

## 8. Cancellation, tenant scope, PII, and sort input

- **Flow `CancellationToken`** through stream copies, blob I/O, image decode, and repository queries. Several
  paths dropped it (issue #0c78e50, "propagate cancellation tokens") — don't reintroduce that. The repository
  methods already take one; pass it on.
- **Preserve tenant scope** on every query, write, and (if ever added) background job. Uniqueness/dedup/listing
  indexes are all `TenantId`-leading (see `multi-tenancy.md`); a query that drops `TenantId` leaks or collides
  across tenants.
- **Don't log file contents, blob bytes, recipient/owner IDs, or verbose PII** outside Development. PII logging
  was restricted to development for a reason (issue #243182b); keep it that way.
- **Dynamic-LINQ `sorting` must be validated against a column allowlist** — never pass a raw client sort string
  to the query (issue #6df4cc8, "allowlist file descriptor sorting"). EF and MongoDB must apply the **same**
  default order (`CreationTime` descending).

## 9. Update vs patch — don't silently clear metadata

The update path must distinguish a **full update** from a **patch**. A rename that unconditionally overwrites
`DirectoryId`/`Name`/`CellName` wipes fields the client never sent (the Angular rename sends only `{ name }`),
clearing the directory relationship and `FileCell` info. Split the two, and **re-validate** directory,
container, tenant, owner, and file-grid constraints on update — an update must not be a back door around the
create-time invariants. (Audit P1-2, guarded by `Update.Tests` — `Rename_ShouldPreserveDirectoryAndCellName`.)

## 10. Keep it thin — the anti-scope list

These have been deliberately kept out; don't add them without a real, in-repo need:

- **No distributed events / outbox / ETOs** in the modules — the pipeline is inline (see `infrastructure.md`).
  Blob/DB consistency is §4's ordering+compensation, not at-least-once transport.
- **No hand-rolled object mapping** — mapping is Mapperly (`FileExplorerApplicationMappers`), moved off
  AutoMapper on purpose; don't reintroduce AutoMapper or a `MapToDto` switch (see `application-layer.md`).
- **No `netstandard` multi-targeting** — every project is `net10.0` only (see `dependency-rules.md`).
- **No new per-aggregate storage abstraction** beyond the two custom repositories — add query methods to
  `IFileDescriptorRepository`/`IDirectoryDescriptorRepository` and implement in both providers, rather than
  inventing a new seam.
- **Don't couple the FileStoring core to `file-explorer`** — the core must keep working standalone (mode 1 in
  `template/app.md`).
