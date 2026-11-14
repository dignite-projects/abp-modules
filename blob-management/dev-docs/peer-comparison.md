# Blob Management against its peers (2026-11-14)

It compares Blob Management with the open-source projects that hold the same position - storage that applications call,
not a drive people use - and lists what the module builds itself where .NET, ASP.NET Core or ABP already has the piece.

## Decided by the owner, 2026-11-14

The four changes at the end of this record, in their order, each its own change. Done so far:

1. **Content is no longer shared between files** (§1). Every descriptor writes a BLOB of its own; `DedupKey`, the dedup
   lock and the check whether a BLOB is still in use are gone, and a BLOB is deleted with its descriptor after commit.
   Usage has one measure, stored bytes, for the tenant, the containers, the users and the clients
   (`UploadedBytes` became `StoredBytes`), and a public address is one file's. The hash stays on the file.
2. **Bytes are served through ASP.NET Core's `FileStreamResult`** (§3), from one base of both addresses
   (`BlobManagementFileReadController`): `Range` and `If-Range`, `Last-Modified` (the file's creation time - its bytes
   never change) and every precondition are the framework's. The `If-None-Match` check before the bytes are opened,
   and `HEAD` without them, stay.
3. **The anonymous addresses name ASP.NET Core rate limiting policies** (§4), which the module registers
   (`BlobManagementRateLimiting`, `BlobManagementRateLimiterPolicy`): a fixed window of a minute per client address,
   answered `429` with `Retry-After`. A public read that asks for a size is a rendition, limited apart from reads as
   stored; the limits are `BlobManagementRateLimitingOptions` (renditions 120, uploads 60, reads as stored and with a
   link unlimited). The host runs `app.UseRateLimiter()` after routing: a module cannot place middleware there.
4. **Buffered copies spill to disk** (§2), in blob-storing: `BlobStreamBuffering.CopyToSeekableAsync` keeps up to
   `BlobStoringPipelineConsts.MemoryBufferThreshold` (64 KB, ASP.NET Core's form threshold) in memory and the rest in a
   temporary file deleted with the copy; the cap is unchanged. `EnsureSeekableAsync`, `MaxSizeContributor`,
   `FileDescriptorManager` and the upload with a ticket copy through it. `CopyToBufferAsync`, published, still returns a
   `MemoryStream`. Resumable uploads stay out until large files are wanted.

Until now the module was compared with ABP's File Management only, which is a personal drive. The projects below are
storage services that business systems integrate with, which is what [`positioning-decision.md`](positioning-decision.md)
and [`business-system-integration-decision.md`](business-system-integration-decision.md) say this module is.

## What was read

| Project | License | Read at | What was read |
|---|---|---|---|
| [Supabase Storage](https://github.com/supabase/storage) | Apache-2.0 | `be3cecb` (2026-11-13) | Schema migrations (`migrations/tenant`), signing and verifying (`src/storage/object.ts`), uploads (`src/storage/uploader.ts`), serving (`src/storage/renderer`), image rendering and its rate limiter (`src/http/routes/render`) |
| [Nhost Storage](https://github.com/nhost/nhost/tree/main/services/storage) | MIT | `9c5a364a96` (2026-11-13) | Schema, upload, presigned URLs, orphans (`controller/`) |
| [Appwrite](https://github.com/appwrite/appwrite) | BSD-3-Clause | `8d5559c` (2026-11-13) | Bucket and file collections, file creation in chunks, file tokens, the usage worker |
| [UploadThing](https://github.com/pingdotgg/uploadthing) | MIT | `2656a9a` (2026-09-30) | The upload and callback handler, signing (`packages/shared/src/crypto.ts`), the SDK's signed URLs and ACL |
| [tusd](https://github.com/tus/tusd) | MIT | `cec3998` (2026-11-05) | Hooks |
| [Azurite](https://github.com/Azure/Azurite), Microsoft's Azure Storage emulator | MIT | `f049fde` (2026-11-10) | Blob SAS verification, public access |
| [Squidex](https://github.com/Squidex/squidex) (.NET) | MIT | `b301e61` (2026-11-11) | Asset folders, the duplicate check, usage tracking |
| [Orchard Core Media](https://github.com/OrchardCMS/OrchardCore) (.NET) | BSD-3-Clause | `4f68cf1` (2026-11-12) | The media token, the rendition cache |

Ideas only; no code was copied. Left out: MinIO (AGPL-3.0, its community edition archived), Seafile and Nextcloud
(AGPL, and drives), Directus (BSL 1.1), Ceph RGW (the storage tier only).

Verdicts below: **Keep** - the peers agree, or the module differs for a reason that holds; **Change** - recommended;
**Owner** - the owner's choice to make.

## 1. Content shared between files - Owner (recommended: remove)

No peer shares stored bytes between two files. Supabase writes every upload to a key of its own
(`{tenant}/{bucket}/{name}/{version}`, `src/storage/locator.ts`) and removes a replaced version with a background job;
Nhost, Appwrite, UploadThing and Azure store each file once. Squidex hashes an upload and, when an asset with the same
hash exists, returns that asset instead of creating one - the caller sees the duplicate and decides
(`AssetCommandMiddleware`, `AssetDuplicate`); its assets never share bytes.

Here content sharing came over from Site's file library with the Drive ([`drive-decision.md`](drive-decision.md)); no
record weighs it. What it costs:

- `FileDescriptorManager` holds a distributed lock per content hash until the unit of work is disposed, a unique
  `DedupKey`, and a deletion that runs after commit under the same lock and asks whether another descriptor still names
  the BLOB. That and the name lock are much of its 946 lines.
- Two measures of usage, stored bytes and uploaded bytes, which do not add up ([`positioning-decision.md`](positioning-decision.md), *Usage*).
- The public address of a file kept by id is its physical BLOB, shared by every file with the same bytes: it answers
  while one of them is public, and with the BLOB's name when more than one is (`BlobManagementPublicAppService.GetPublicFileAsync`).
- Site's migration has to carry rows whose bytes sit under another row's BLOB name (*Proposed* in `drive-decision.md`).

What it saves: the bytes of the same content stored twice in one container of one tenant. A business system's files
rarely repeat; where they do (one logo on many records) the business system can keep one file and refer to it.

Without it, each descriptor owns its BLOB; the dedup lock, `DedupKey` and the check after commit go; usage has one
measure; a public address names one file. Kept: the SHA-256, as an `ETag` and for a Squidex-style duplicate check if a
business system asks for one; the name lock and `NameKey`, which serialize overriding saves under one name as
Supabase's advisory lock on an object name does (`waitObjectLock` in `Uploader.completeUpload`); the deletion after
commit, best-effort and without the lock - Supabase queues the deletion of replaced bytes the same way, only after its
transaction committed, and logs a failure as a recoverable orphan.

## 2. Uploads are buffered in memory - Change (in blob-storing)

Supabase streams the request body to the storage backend and counts bytes on the way (`ByteCounterStream`). Appwrite
takes chunks with `Content-Range` and keeps `chunksTotal` and `chunksUploaded` on the file, so an upload resumes.
Supabase also speaks tus, whose reference server is tusd.

Here ASP.NET Core's form reader buffers an upload to disk, then blob-storing's `BlobStreamBuffering.CopyToBufferAsync`
copies it into a `MemoryStream`, capped by the container's `MaxSize` or 100 MB (`BlobManagementUploadController`,
`FileDescriptorManager.StoreOrShareAsync`): every concurrent upload can hold its whole size in memory, and nothing
resumes. The pipeline re-reads the bytes (detection, hash, image contributors), so a buffer stays; it need not be memory.

- **A buffer that spills to a temporary file** past a threshold: `FileStream` with `FileOptions.DeleteOnClose`, from the
  BCL. ASP.NET Core has the same thing (`FileBufferingReadStream`, `Microsoft.AspNetCore.WebUtilities`), but it is in the
  ASP.NET Core shared framework, which blob-storing and a Domain layer must not reference.
- **Resumable uploads**, only if large files are wanted. Microsoft has no tus implementation;
  [tusdotnet](https://github.com/tusdotnet/tusdotnet) (MIT, community) is the .NET one. Its hooks map onto the ticket:
  verify it before the upload is created, store the file when it finishes.

## 3. Serving: conditional requests by hand, no ranges - Change

Supabase and Nhost answer `Range` (`Accept-Ranges: bytes`, `206`, `Content-Range`), `If-None-Match` and
`Last-Modified`. A video, an audio file or a long PDF needs ranges to seek.

`BlobManagementPublicController` and `BlobManagementReadLinkController` compare `If-None-Match` themselves, set no
`Last-Modified`, return `FileStreamResult` without range processing, and repeat the same header code. ASP.NET Core's
`File(stream, contentType, lastModified, entityTag, enableRangeProcessing: true)` implements the preconditions
(`If-None-Match`, `If-Modified-Since`, `If-Match`, `If-Unmodified-Since`, `If-Range`), `Range` and `HEAD`. Keep the
`If-None-Match` check before the bytes are opened - opening is a request to the storage backend in ABP's S3, MinIO and
Azure providers - and hand the rest to the framework, in one helper for both controllers. Ranges then work where the
provider's stream can seek (the file system, Azure's `OpenReadAsync`); the S3 and MinIO response streams cannot, and
`IBlobContainer` has no ranged read - a request for ABP, not something to build here.

## 4. Anonymous endpoints have no rate limit - Change

Supabase rate-limits image rendering per tenant, client and path (`src/http/routes/render/rate-limiter.ts`) and puts a
CDN in front. `/blobs` renders a size on every request - the list of sizes bounds what can be asked, not how often - and
the upload and link endpoints are anonymous as well. ASP.NET Core has rate limiting built in
(`Microsoft.AspNetCore.RateLimiting`): the module names a policy on each anonymous endpoint (`[EnableRateLimiting]`) and
registers defaults for it; the host turns the middleware on and tunes them.

A server-side cache of renditions stays *not chosen* - the CDN's job in front of `/blobs`. If a deployment has no CDN,
ASP.NET Core's output caching (`Microsoft.AspNetCore.OutputCaching`) is the piece, not a cache of the module's own.

## 5. Browser credentials - Keep

| | Signed how | Same file, same address | Revocable |
|---|---|---|---|
| Supabase | JWT (HS256) with a signing key per tenant: scope (`download`, `upload`), path, expiry | No | No |
| Nhost | The S3 backend's presigned URL (SigV4), proxied | No | No |
| UploadThing | HMAC-SHA256 of the URL with the app's API key, signed by the business back end through the SDK | No | No |
| Azure (Azurite) | HMAC-SHA256 over a string to sign | For the same expiry | Through a stored access policy (`si`) |
| Appwrite | A JWT naming a stored token row, `iat` the row's creation | Yes | Yes, by deleting the row; a read per request |
| Orchard Core | HMAC-SHA256 of the resize commands, a random key in the site settings | Yes | By rotating the key |
| Blob Management | Data Protection's time-limited protector, keys in the host's key ring | Within a window, through the distributed cache | No |

Data Protection is Microsoft's primitive, with key rotation and a key ring the host persists; the others make the
service manage a key (Orchard Core, UploadThing) or a table of tokens (Appwrite). The cache that keeps a link stable is
a few lines. Tickets stay the module's to issue rather than the business system's to sign (UploadThing's way):
UploadThing can let a back end sign because every back end runs its SDK with a secret shared with the service, which
here would be a second secret per client beside its OAuth credentials.

Seen and not adopted: revocation (Azure's stored access policy, Appwrite's rows), since a link lives minutes; Azure's
signed response headers (`rscd`, `rsct`), a download name per link.

## 6. Who makes a file public - Keep

Per container in Supabase (`buckets.public`) and Azure (a container's public access level); per file in UploadThing
(`acl`, changed with `updateACL`) and Appwrite (file permissions when `fileSecurity` is on). The module's mark on the
file, set by the business system (`FileDescriptor.IsPublic`), is UploadThing's model. With §1, a public address names
one file.

## 7. Confirming an upload - Keep

UploadThing's service calls the business back end when an upload finishes (`onUploadComplete`, a request signed with
the API key), and the browser can wait for the back end's answer (`awaitServerData`). tusd calls hooks (`pre-create`,
`post-finish`). Nhost marks a row `is_uploaded` only to make the write atomic, and gives administrators endpoints that
list the uploads that never finished and delete orphaned bytes. Supabase has no such step: an upload is final.

Here the business system pulls: its browser hands back the file id, it checks the uploader and calls `ConfirmAsync`;
what is never confirmed is deleted after `PendingLifetime`. A push would need the module to know a callback address per
client, sign the call and retry it; the pull keeps the module passive. Keep.

## 8. Directories - Keep

Supabase's folders are name prefixes. It added a `prefixes` table to list them faster (migrations 0026 to 0041), rolled
back its triggers (0042) and dropped the table (0052); renaming a folder there renames every object under it. Appwrite,
Nhost and UploadThing have no folders; Azure Blob has virtual directories by delimiter. Squidex keeps folders as
entities with a parent (`AssetFolder`), as this module does. A file here refers to its directory by id, so renaming or
moving a directory changes one row - what Supabase's prefixes could not give.

## 9. Usage - Keep; watch the allocation check

Supabase sums object sizes per bucket when asked (`storage.get_size_by_bucket()`), as this module does. Appwrite (its
usage worker's `files.storage`) and Squidex (`AssetUsageTracker`, per app and day, from created and deleted
events) keep counters. Here the allocation check sums the tenant's descriptors on every store
(`BlobManagementUsageManager.CheckCanStoreAsync`, `GetUsedStorageSizeAsync`): exact, with no second record to keep in
step, and slower as files accumulate. When that shows, a counter per tenant written in the store's unit of work is the
peers' answer. With §1, the query loses its `DISTINCT`.

## 10. Container settings and type detection - Keep

Supabase (`file_size_limit`, `allowed_mime_types`), Nhost (minimum and maximum size, `cache_control`,
`download_expiration`, `presigned_urls_enabled`) and Appwrite (`maximumFileSize`, `allowedFileExtensions`, compression,
encryption, antivirus, transformations) keep these on the bucket row; here they are the container's configuration and
pipeline contributors, ABP's container model. The module detects a type from the bytes, as Nhost does; Supabase trusts
the request's `Content-Type` and Appwrite checks the extension. Not here and not asked for: an antivirus scan (Appwrite
and Nhost call ClamAV), which would be a pipeline contributor in blob-storing.

## 11. Public sizes - Keep

Supabase (through imgproxy) and Nhost accept any size between a minimum and a maximum and lean on a rate limit and a
CDN; Orchard Core signs the resize parameters of the URLs it generates (`MediaTokenService`) and caches renditions on
disk. The module's list of sizes needs no key and no cache; §4 adds the rate limit.

## What the module builds where a foundation exists

| Built here | Foundation | Verdict |
|---|---|---|
| `If-None-Match` and `HEAD` by hand, no ranges, in two controllers | ASP.NET Core `FileStreamResult` (`EntityTag`, `LastModified`, `EnableRangeProcessing`) | Change (§3) |
| Nothing against floods on the anonymous endpoints | ASP.NET Core rate limiting | Change (§4) |
| An upload buffer in memory (blob-storing) | A BCL `FileStream` with `DeleteOnClose`; `FileBufferingReadStream` is out of a Domain layer's reach | Change (§2) |
| Content sharing under distributed locks | Nothing to replace it with: no peer has it | Owner (§1) |
| Signing tickets and links | ASP.NET Core Data Protection, already | Keep |
| A resumable upload | None from Microsoft; tusdotnet (MIT) | Only if large files are wanted |
| A rendition cache | ASP.NET Core output caching | Not chosen; the CDN's job |

On foundations already: hashing (`SHA256`), headers (`Microsoft.Net.Http.Headers`), locks, background workers, the
distributed cache, features and permissions (ABP), resizing (ABP's `IImageResizer` on SkiaSharp), type detection
(FileSignatures, MIT, in blob-storing; .NET has no content sniffer).

For ABP: a ranged read on `IBlobContainer` (§3).

## The changes

Each its own change, in this order:

1. Remove content sharing (§1): the descriptor, the manager, usage, public addresses, the tests and the documents.
   Nothing is published, so nothing migrates.
2. Serve through `FileStreamResult` with ranges, one helper for both controllers (§3).
3. Rate-limit policies on the anonymous endpoints (§4).
4. A buffer that spills to disk, in blob-storing (§2).
