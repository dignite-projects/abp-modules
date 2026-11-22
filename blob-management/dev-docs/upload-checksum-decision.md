# Upload checksum: verified when the caller sends one (2026-11-20)

> The paths below that name the BLOB API and the file API (`api/blob-management/blobs`, `.../files`) are
> `integration-api/blob-management/...` since 2026-11-22 ([`design-review-decision.md`](design-review-decision.md)).

> Partly replaces [`peer-comparison.md`](peer-comparison.md) (2026-11-14), which kept the SHA-256 on every file "as an
> `ETag` and for a duplicate check if a business system asks for one". The code never used it as an `ETag` (the `ETag`
> is the file's physical BLOB name), nothing queries by it, and its index went with `DedupKey`. What is written there
> about keeping the hash stands only as far as this record keeps it.

Status: decided by the owner on 2026-11-20 (the hash is computed when the caller asks for it, not on every upload; a
caller that stores through ABP's `IBlobContainer` gets no check) and implemented the same day. Where the code differs
from what *Design* first proposed, the point says so.

## The problem

`FileDescriptorManager.StoreAsync` hashes every upload with SHA-256 after the content is buffered, stores the value in
`FileDescriptor.Hash` (non-null, 64 characters, upper-case hex) and returns it in `BlobManagementBlobDto.Hash`. Three
facts about that:

- **It verifies nothing.** The hash is computed from what the server received and handed back afterwards. At the one
  moment an integrity check can mean something - the caller still holds the original and the server holds what arrived
  - nothing is compared. A truncated or corrupted upload gets a valid-looking hash.
- **Nothing reads it.** No query, sort or `ETag` uses it; the only consumer is the BLOB DTO. The index on it is already
  gone. A business system that stores through ABP's `IBlobContainer` never even sees it: the provider drops the DTO.
- **It costs the larger share of the server's work after the body arrives.** Measured below: about 3 seconds per GB,
  two thirds of buffering + hashing + saving, and it comes after the last byte, so the caller waits for it.

### Measurement (2026-11-20)

A console program on the development machine (12 cores, Windows 11, local SSD, .NET 10, Release) replayed the I/O shape of
one upload of N bytes, each stage timed alone, median of three runs: buffering into a temporary file as
`BlobStreamBuffering.CopyToSeekableAsync` does (80 KB chunks, `DeleteOnClose`, `Asynchronous`); `SHA256.HashDataAsync`
over that seekable file as `ComputeHashAsync` does; `CopyToAsync` into a destination file as ABP's file system provider
does.

| Size | Buffer | SHA-256 | Save | SHA-256 share |
|---|---|---|---|---|
| 100 MB | 53 ms | 280 ms | 80 ms | 68% |
| 500 MB | 264 ms | 1,386 ms | 369 ms | 69% |
| 1 GB | 546 ms | 3,087 ms | 835 ms | 69% |
| 2 GB | 1,179 ms | 5,877 ms | 1,817 ms | 66% |

`SHA256.HashData` over a 64 MB array in memory ran at 445 MB/s on this machine, so the hash is CPU-bound, not I/O-bound,
and grows linearly. Limits of the measurement: a simulation, not the module and not a network; the temporary file was
still in the operating system's cache when it was read back, so under memory pressure the read costs more; the host
and the Angular dev server were running meanwhile. Whether a 3 s/GB wait matters depends on the network: after a
100 Mbit/s upload of 1 GB (about 80 s) it is a few percent, after a gigabit one (about 8 s) it is more than a quarter.

## How the peers do it

Researched 2026-11-20 from the primary pages named below. MinIO and Appwrite were not checked. Nothing here is copied
code; the designs are borrowed.

| | How the caller sends it | On mismatch | Stored / returned |
|---|---|---|---|
| **Amazon S3** ([checking object integrity](https://docs.aws.amazon.com/AmazonS3/latest/userguide/checking-object-integrity.html), [PutObject](https://docs.aws.amazon.com/AmazonS3/latest/API/API_PutObject.html)) | One header per algorithm, `x-amz-checksum-sha256`, `-crc32`, `-crc32c`, `-crc64nvme`, `-sha1`; optional; a trailing checksum after the body for streaming (`aws-chunked`, `x-amz-trailer`) | Rejected before storing, `BadDigest`; the status code was not found on the pages read | Stored with the object, returned on GET/HEAD only when `x-amz-checksum-mode` asks; newer SDKs send a CRC by default, and S3 adds CRC64NVME when none was sent |
| **Azure Blob** ([Put Blob](https://learn.microsoft.com/en-us/rest/api/storageservices/put-blob), [Put Block](https://learn.microsoft.com/en-us/rest/api/storageservices/put-block)) | `Content-MD5` or `x-ms-content-crc64` (both together is a 400), optional | 400 | Put Blob: the service computes and returns its own when none was sent; Put Block: checked in transit and not stored |
| **Google Cloud Storage** ([data validation](https://docs.cloud.google.com/storage/docs/data-validation)) | `x-goog-hash: crc32c=...` (MD5 too), optional; for a resumable upload on its last request, covering the whole object | 400, object not stored | Validated only when sent (and on copy); Google recommends CRC32C |
| **tus** ([checksum extension](https://tus.io/protocols/resumable-upload#checksum)) | `Upload-Checksum: <algorithm> <base64>` per PATCH request, `sha1` required, algorithms listed in `Tus-Checksum-Algorithm` | 460, chunk discarded; 400 for an unsupported algorithm | Per chunk only, no whole-object value |
| **HTTP, RFC 9530** ([Digest Fields](https://www.rfc-editor.org/rfc/rfc9530.html)) | `Content-Digest: sha-256=:<base64>:` over the content after content coding; `Want-Content-Digest` states preference; active algorithms `sha-256`, `sha-512` | The RFC defines no behaviour and no status code | Not addressed |
| **Supabase Storage** ([`uploader.ts`](https://raw.githubusercontent.com/supabase/storage/master/src/storage/uploader.ts), [`s3-handler.ts`](https://raw.githubusercontent.com/supabase/storage/master/src/storage/protocols/s3/s3-handler.ts)) | Its S3 endpoint ignores `ChecksumSHA256`, `ContentMD5` and `x-amz-checksum*` | None | Copies the backend's values on completing a multipart upload, without comparing |

What they agree on: the check is **optional**, made **by the server on receipt**, **before the object is stored**, and a
mismatch is a **400-class error** (tus alone uses its own 460). Where they differ: the algorithm (S3 and GCS lean to CRC
for speed; RFC 9530 lists `sha-256` and `sha-512` as active and `crc32c` as deprecated; tus requires SHA-1); whether the
service also computes a value when none was sent (Azure and S3 do, for their own metadata; GCS does not); and whether
the value is kept (S3 and Azure for a block blob yes, Azure's Put Block and tus no). Found and not found: S3's page shows a base64
value in its trailer example and a hex one in the PutObject sample, so its encoding is not stated outright; Azure's
`Md5Mismatch` / `Crc64Mismatch` error names were not on the pages read; GCS's page does not say "base64" outright.

**Browsers.** `SubtleCrypto.digest()` takes a whole buffer and no stream
([MDN](https://developer.mozilla.org/en-US/docs/Web/API/SubtleCrypto/digest)), so a browser cannot cheaply hash a large
`File` with it. A direct upload from a browser can send a checksum for small files, and for large ones only with a
JavaScript or WebAssembly incremental hasher.

## Design

**Decided by the owner, 2026-11-20:** the SHA-256 is computed when the caller sends one to be verified, and only then.
An upload without one costs no hash.

Proposed for the rest:

1. **Optional, on every way in that stores bytes - except ABP's `IBlobContainer`.** The BLOB API
   (`PUT api/blob-management/blobs`), the file API (`POST api/blob-management/files`) and the browser's upload with a
   ticket (`POST api/blob-management/uploads/{ticket}`), and the in-process `IBlobManagementBlobService.SaveAsync` /
   `IBlobManagementFileService` for the same caller. All of them reach `FileDescriptorManager.StoreAsync`, which is
   where the check lives, so there is one place and one rule. The provider path of `IBlobContainer` has no argument
   to carry a value and gets no check (*The ABP `IBlobContainer` path*, below).
2. **SHA-256 only.** One algorithm, as asked; the caller sends a value, not a choice. Others (CRC32C, XXH3) can be added
   as further algorithms later without changing the shape, as S3's one-header-per-algorithm does.
3. **Wire format.** Over HTTP, a request with the file as its body carries `Content-Digest: sha-256=:<base64>:`
   (RFC 9530), the standards-track spelling, and the value is base64 as S3, RFC 9530 and tus all use. A multipart form
   upload cannot use it: `Content-Digest` covers the whole multipart body, not the file in it. There the file's value is
   a form field of its own. **No peer was found that settles the multipart case**, so that part is this module's own
   design; it is kept to one field, `sha256`, base64. As built: the upload with a ticket reads `Content-Digest` for a
   raw body and the form field `sha256` for a multipart one; the file API's form is its input DTO, so the field is its
   `Sha256` property (ASP.NET Core binds `sha256` to it); the BLOB API takes `sha256` in the **query string**, like
   its other simple parameters, because the remote client's call is multipart with the file as the only part and the
   rest in the query - the generated proxy sends it there with no special case.
4. **Verified before anything is written.** `StoreAsync` compares after buffering and before the quota check and
   `container.SaveAsync`: a mismatch leaves no BLOB and no descriptor, so there is nothing to compensate. The hash is
   of the bytes the caller sent, taken before the pipeline runs - a JPEG the container stores as WebP still matches
   what the caller hashed.
5. **Computed while receiving where it can be.** When the check is asked for and the incoming stream is not seekable,
   it is wrapped in a read-through stream that feeds an `IncrementalHash` as `EnsureSeekableAsync` reads it, so the hash
   hides behind the network and adds almost nothing after the last byte. When the stream is already seekable
   (`BlobStreamBuffering` uses it as it is), it is one pass over it, as `ComputeHashAsync` was. The upload with a
   ticket is the one way in that does not get the first: `BlobManagementUploadAppService` buffers the body itself
   (to check the ticket's size and allowed types) before it calls the manager, so there the manager sees a seekable
   stream and takes the pass of its own. A ticket's size is bounded, so that pass is short.
6. **A mismatch is a 400.** A `BusinessException` with a new error code `BlobManagement:ChecksumMismatch`, data
   carrying both values, mapped to `400 Bad Request` as S3, GCS and Azure answer. A `Content-Digest` or form value that
   cannot be read (not base64, wrong length) is the same 400 with `BlobManagement:ChecksumInvalid`. A `Content-Digest`
   that names only algorithms the module does not check (for example `md5`) is refused with the same code rather than
   ignored: the caller asked for a check the module cannot make, and it must not pass as if it had. RFC 9530 allows a
   recipient to ignore a digest it does not support; this is the module's choice, and a stricter one. A `Content-Digest`
   with both `sha-256` and `sha-512` is checked on `sha-256` and the rest ignored.
7. **What is stored.** `FileDescriptor.Hash` becomes nullable and holds the value that was verified, base64, as it was
   sent (the column's 64 characters hold 44). It is `null` for a file stored without one. `BlobManagementBlobDto.Hash`
   is `string?` the same way. Base64 replaces the upper-case hex the column held, so the string a caller sent
   is the string it reads back; the module is unreleased, so this is one migration of the host
   (`Made_File_Hash_Optional`, which also clears the old values: they were in the other format and had been checked
   against nothing) and no upgrade path. The file API's `BlobManagementFileDto` never carried the hash and still does not.
8. **The unconditional pass goes.** `ComputeHashAsync` is called only when a value was sent. The `Hash` column keeps
   no index and no sort.

### Not part of this change

- **Reads.** Returning the stored value as `Repr-Digest` on a file's content, or on the file API's DTO, is a separate
  question; nothing asks for it yet.
- **A module-computed default.** Azure and S3 compute a value when none is sent because their own metadata and
  `ETag`s need it. This module's `ETag` is the BLOB name, so it has no such need, and a default would put the cost
  back.
- **A duplicate check.** `peer-comparison.md` kept the hash for a business system that asks for one. With the hash on
  only some files, that is no longer something the module can offer, and a business system that wants it computes its
  own, as `blob-storing`'s "what stays out" says for hash sidecars.
- **Per-chunk checks and resumable uploads.** The module has neither; tus's and GCS's resumable cases do not apply.

## Not chosen

- **Keep hashing every upload.** It costs 3 s per GB for a value nothing reads and no check uses. Kept only if the
  owner wants every file to carry a hash for a later purpose; then the read-through hash of point 5 would apply to every
  upload instead of the ones that ask.
- **Hash only the head of a large file.** Two files with the same first bytes and different endings would carry the same
  value, a truncated upload would verify, and one field would mean two algorithms depending on size. It fails the one
  thing a hash is kept for.
- **A faster algorithm in place of SHA-256 (CRC32C, XXH3).** S3 and GCS lean to CRC for speed, and a CRC is enough for
  accidental corruption. Not chosen now because the request is for SHA-256, and RFC 9530 lists `crc32c` as deprecated,
  `sha-256` as active. The wire format leaves room for it.
- **S3's own header, `x-amz-checksum-sha256`.** Works, and S3 clients already send it. `Content-Digest` is the
  standard spelling and needs no vendor prefix; S3 compatibility is not a goal of this module.
- **The hash as a second step after the upload** (a later `POST .../verify`). The caller would learn of a bad upload
  only after it was stored and counted against the allocation.

## Cost

- A migration (`Hash` nullable, base64) and a test change: `FileDescriptorManager_Tests`, `UploadedFiles_Tests`,
  `BlobManagementPresets_Tests` and `BlobManagementFileApi_Tests` assert on `Hash`.
- New: the error codes in all 25 cultures, the optional parameter on the in-process and remote services (and on the
  C# client proxies, which are generated), the form field on the two multipart entries, the read-through stream.
- Browser direct uploads will rarely send a value for large files: the check is there for back ends and for small
  files, which is what the peers' optional checks are for too.
- The C# client proxy `RemoteBlobManagementBlobService` has to send the header or form field when its caller passes a
  value.
- A business system that stores through `IBlobContainer` gets no upload check; see the next section.

## The ABP `IBlobContainer` path: no check (decided by the owner, 2026-11-20)

A business system that stores through ABP's `IBlobContainer.SaveAsync(name, stream)` reaches the module through
`BlobManagementBlobProvider.SaveAsync`. ABP hands a provider a `BlobProviderSaveArgs` - container, name, stream, whether
to overwrite - with no place for a checksum, and `SaveAsync` returns nothing. So on this path **nothing is computed and
`Hash` stays `null`**. A business system that wants the check calls `IBlobManagementBlobService` or
`IBlobManagementFileService` directly, which take the expected value as an optional argument; a caller that passes one
gets it verified, a caller that passes none costs the server no hash.

Today this path pays for nothing: the provider drops the `BlobManagementBlobDto` that `SaveAsync` returns
(`BlobManagementBlobProvider.cs:49`), so a caller that stores through `IBlobContainer` never sees the hash the server
computed and stored. That is one more reason the unconditional pass goes.

Not chosen: a container option under which the provider hashes the stream itself before sending it, so the server
verifies it. The caller there is the business system's back end - the process that hosts the provider package, such as
Site's - not a user's machine. It would hash the same stream twice (once there, once in the module) and prove less
than it seems:

- **In-process** (the host installs `Dignite.Abp.BlobManagement.Application`), the bytes never leave the process between
  the two hashes: nothing can go wrong between them for the check to catch.
- **Remote** (`Dignite.Abp.BlobManagement.HttpApi.Client`), the hop is HTTPS, which already protects it against
  corruption in transit. A hash taken by the back end shows that what it sent is what the module received. It does not
  show that the file the user uploaded is the file stored, because the leg from the user to the back end has no value
  to compare. That end-to-end check needs the value to come from the browser or from the file's source, which only the
  business system has, and it passes it through the service interfaces, which take it.

`IBlobContainer` has no notion of a checksum either, so a provider option would be this module's own extension of
ABP's abstraction, and one that closes only the least useful leg. If a business system shows a need for it, the option
can be added then without changing the service interfaces.
