# Blob Management: the design review of 2026-11-22

> **Amended 2026-11-23** ([below](#amendment-2026-11-23-a-files-life-is-its-records)): a file kept by id and a
> directory are soft-deleted again, a file keeps its bytes until its business system deletes it for good, and a
> directory has no name key. What *A deleted descriptor is removed* says further down is history.

Status: decided by the module's maintainer on 2026-11-22 - the owner asked for the code base to be looked at again, for
compromises and patches left by the changes of direction since 2026-11-10, and left the design to the maintainer -
and implemented the same day in three batches, in the working tree (not committed). Nothing was published under the
old shapes except the pre-releases of `10.7.7`, which this changes without a way back: the Migrate notes are in
`CHANGELOG.md`.

The module changed direction five times in twelve days (a drive, Blob Management, no file API for users, the
integration of 2026-11-14, one BLOB per file). Each change was made against the code of the one before, with the
earlier shape kept where removing it was not needed. This review asked, for every piece, one question: *would it be
written like this if the module started today, with nobody to keep compatible?* What answered no is below.

## First batch: defects, and what the changes of direction left

Five defects, none of which changed a contract:

| | What it was | What it is |
|---|---|---|
| Lookup by name | `FindByNameAsync` compared the `Name` column. The name key (a hash of tenant, container and name) is what the database keeps unique, and what the name lock is named by; the column compares as the database's collation does, which on SQL Server and MySQL ignores case. `A.png` and `a.png` could both be saved, and the next overriding save of `a.png` could delete `A.png`. The tests run on SQLite and MongoDB, which compare exactly, and could not show it | `FindByNameKeyAsync(nameKey)`. A name is looked up by its key, in the manager, and nowhere else |
| A BLOB outlives its rollback | The bytes are written before the row, inside the caller's unit of work. If that unit of work failed after the save returned, the row went and the bytes stayed, counted for nobody | `DeleteBlobOnRollback`: hung on `IUnitOfWork.Failed`, and only where a failure does undo the write (`IsRolledBackOnFailure`: a transactional unit of work), and never after the commit - ABP raises `Failed` for an `OnCompleted` handler that throws too |
| The provider dropped the caller's token | `IBlobManagementBlobService` is an application service and takes no cancellation token | The provider sets the ABP ambient token (`ICancellationTokenProvider.Use`) around each call; the in-process service reads it. No contract changed |
| Remote and in-process counted different users | In process, ABP's auditing records the creator only when the current user belongs to the entity's tenant; the remote services always sent the current user, and rewrote the caller's input | The remote services apply the same rule, and send a copy |
| The directory tree's `manage` input | What was left of the file picker. Nobody set it, and it kept a drag handle and an empty menu on every node for users who may change nothing | Removed; the menu and dragging follow what the container's `access` grants |

And what the changes of direction left, removed: `ObjectUrlService` and the `blob:` / `data:` branches of the file
URL service (left from uploading in the browser), a `JSON.stringify` that held the browser's state when the container
object changed (the three components now reset on the container's *name*), the Settings and Mapperly template shells,
Drive-era names, one test double that re-implemented the public read it was testing, and the README of the Angular
library, which was the CLI's.

## Second batch: what a file's row is

The three decisions of this batch are one design - what a descriptor is - and were taken together.

### Saving over a name changes the row

Before: an overriding save wrote a *new* descriptor and deleted the old one. Both could not hold the name key, so the
old one gave it up first (saved on its own, because ABP's EF Core soft delete reloads the row), the new one was
inserted, and a failure gave the key back (`TryClaimNameKeyAsync`) - a best-effort step that, without a transaction,
could leave a file live but without its address.

Now: the descriptor found under the name takes the new content (`FileDescriptor.ReplaceContent`): the BLOB, type,
size, hash, writer and public mark. It stays the same file - same id, same name, same key - in one write of one row.
The BLOB it had is deleted after the commit, as a deleted file's is. A failure leaves the file as it was, in memory
too. There is no key to give up or take back.

Options weighed:

- *Insert, then delete (before).* Two rows and a key handed between them; the cost is the compensation above.
- *Delete, then insert.* The name is free for a moment, and a failure after the delete loses the file.
- *Update the row (chosen).* ABP's own database BLOB provider (`DatabaseBlobProvider.SaveAsync`) does this: it
  finds the BLOB by its name and sets its content. Read in ABP's source, LGPL-3.0, for the design only. The peers of
  [`peer-comparison.md`](peer-comparison.md) §1 key a replaced object by a new version and delete the old bytes
  afterwards, which is the same row-and-bytes split (that record was not read again for this one).

What it costs, and how it is met:

- **Another overriding save may read the same row.** The name lock still takes turns on it. Where the lock cannot be
  had, the row's concurrency stamp arbitrates, as the unique name key does for two first saves. No test covers the
  path without the lock.
- **`CreationTime` and `CreatorId` are the save's, not the first save's.** The file is counted - in the usage by user,
  and as `Last-Modified` of a read - for whoever stored the bytes it holds now, as it was when each save made a new
  row. A caller that names the user (`SaveBlobArgs.CreatorId`) keeps naming one.
- **A read in flight can lose its BLOB.** A read that found the descriptor, and then no bytes under its BLOB name, was
  overtaken by a save that deleted that BLOB with its commit. By the reading of the code this was so before - the old test deleted the old
  BLOB later than a real concurrent save does - and it is no longer answered as a missing file: `FindReplacedBlobAsync` looks the
  name up once more, in a unit of work of its own, and the whole answer is built again from what the file holds now.
  It has no precedent that was found; it is this module's own, small, and tested on EF Core and MongoDB.

### A deleted descriptor is removed

Before: both descriptors were soft-deleted, and the bytes were deleted for good. The module has no trash, no restore
and no reader of a deleted row but one (an upload ticket's replay check). Every overwrite and delete left a row that
nothing could use, none was ever reclaimed, and the usage queries walked past them.

Now: `FileDescriptor` and `DirectoryDescriptor` are removed by the delete (`AuditedAggregateRoot` for the directory;
the file loses `IDeletionAuditedObject`). A file's name key, once set, is kept for as long as the file exists; a
directory's name key is required. `IsAddressedByName` no longer changes during a file's life.

Cost: an upload ticket is no longer refused after its file is deleted. A ticket stores one file, *while that file
exists*; once it is deleted - by its business system, or as abandoned - the ticket can store another within its
lifetime, pending, under the ticket's size and type limits, and a ticket lives minutes. It was judged cheaper than a
tombstone table for the replay check, and it is the one place the old behaviour is weaker.

The migration of the demo host deletes the rows marked deleted before it drops the mark: their BLOBs are gone, and
without that they would come back as files without bytes. A project that has run a pre-release does the same
(`CHANGELOG.md`).

### The manager's entries say what they do

`FileDescriptorManager.CreateAsync` and `UploadAsync` were named for who calls them once (a BLOB API, an uploader); both
take the other's options, and `UploadAsync` took an `Action<FileDescriptor>` to set what its caller needed. They are now
`SaveBlobAsync(container, name, stream, SaveBlobArgs)` - a BLOB under a name - and `CreateFileAsync(container, name,
stream, CreateFileArgs)` - a file kept by id - which share `StoreBlobAsync` (buffer, detect, verify, check the
allocation, save the bytes) and `RecordAsync` (write the row, and delete the bytes when the write does not last). The
callback is gone; what it was for - extra properties written with the row, which an application that migrates its
own file library in uses for the path `DirectoryPathImporter` files by - is `CreateFileArgs.ExtraProperties`. The
dependencies that were resolved lazily "so that the constructor stays as it was" are injected: the only subclasses
were test doubles, and the module has no outside subclasses to protect.

## Third batch: the BLOB API and the file API are integration services

Before: `api/blob-management/blobs` and `api/blob-management/files` were ordinary APIs of the module, called by
back ends, authorized by a client's grant on the container, and exposed by every host that installs the HTTP API - an
application that hosts the module in its own process, with no caller for them, exposed them too. The file API had two
kinds of caller, a client for everything and a user with the management permission for the reads, which the service
told apart by whether the token carried a user. The service that authorized *inherited* the trusted one and overrode
its methods one by one to add a check (the read links of a list through a protected hook): a method added to the base
and not overridden would have been open to any signed-in user. No hole existed - every method of the contract was
covered - and nothing checked that.

Now:

- **The two APIs are ABP integration services** (`[IntegrationService]`):
  `IBlobManagementBlobIntegrationService` and `IBlobManagementFileIntegrationService` at
  `integration-api/blob-management/...`. ABP hides integration controllers unless the host sets
  `AbpAspNetCoreMvcOptions.ExposeIntegrationServices` (`AbpServiceConvention`), leaves them out of audit logging by
  default, and its C# proxy generator includes them where the Angular one, by default, does not. That is what ABP's own modules do for what only back ends call
  (`IIdentityUserIntegrationService`, read in the Identity module of the local ABP clone); ABP's documentation
  describes exactly this split for a reusable module in a monolith (called in process) and as a service (exposed).
- **`IBlobManagementBlobService` and `IBlobManagementFileService` stay as they were.** They are what a business
  system's code injects, and the in-process or remote implementation behind them is the host's choice. The integration
  contracts are derived from them, because this module has the one thing ABP's modules do not: its own host runs both
  the trusted implementation and the authorizing API at once. Only the derived ones carry the attribute.
- **A user's reads are a service of their own**: `IBlobManagementFileAppService`, `api/blob-management/files`, a list,
  one file, its bytes, for `BlobManagement.Containers`. The old name now means something else - the client's contract
  was renamed instead, since the user's service is the one the Angular proxy carries. A token without a user is refused
  there, as it is by the directory, container and usage APIs.
- **The services that authorize do not inherit the trusted ones.** The three share a base with *protected* members only
  (`BlobManagementBlobServiceBase`, `BlobManagementFileServiceBase`), and each writes every method of its contract,
  authorizing its own caller. A method added to a contract has to be written in each service before the module
  compiles. `BlobManagementServiceShape_Tests` pins both halves: no authorizing service is assignable to a trusted one,
  and the bases declare no public method.
- The upload ticket's issuing moved to `BlobManagementUploadTicketIssuer`, beside `BlobManagementReadLinkIssuer`.
- **The Angular library has no proxy for them** - ABP generates Angular proxies for application services only, and a
  browser does not call these - and its generated proxies became a secondary entry point,
  `@dignite/ng.blob-management/proxy`, generated into `proxy/src/proxy/`
  (`abp generate-proxy -t ng ... --entry-point proxy`; the folder has no `lib` level, unlike ABP's own packages: the
  schematics of `@abp/ng.schematics` 10.7.1 build the path without one), beside `/config`, as ABP's identity and tenant
  management packages split them. The policy names moved to `/config`, where the menu that uses them is.

Costs:

- **The URLs moved.** `site` and any other pre-release user change two prefixes, and a Blob Management run as its own
  service must set `ExposeIntegrationServices` and keep `/integration-api/blob-management` away from browsers at its
  gateway. The demo host exposes them, because it is where the proxies are generated from.
- **A client's calls are no longer audit-logged** unless `AbpAuditingOptions.IsEnabledForIntegrationServices` is set.
  The usage by client is read from the descriptors, not from the audit log, and is unaffected.
- **The remote services stay registered by hand.** The generated proxies register themselves under the base
  contracts - the same `IBlobManagementBlobService` - and the module replaces them with the `Remote*Service` wrappers,
  which make a missing BLOB `null` again and send the creator. The review had listed this as removable; it is not.

## Looked at, and left as it is

- **The name lock.** Saving under a name takes turns on it, as Supabase does on an object's name; ABP's database
  provider does not lock. The module has both a lock and a unique key, and a unique key alone fails the loser of two
  simultaneous saves instead of ordering them.
- **A file's public address, its BLOB name.** Generated, random, with the type's extension; renaming or moving never
  breaks a published link. A file saved under a name has its name.
- **Reading an image back after it is stored**, for its size and type. Cheaper options need the pipeline to report
  what a contributor produced; not looked at.
- **`BlobManagement.Container.Files` not limited to the client provider.** It works around ABP's resource permission
  dialog, which saves every permission of a resource for whoever it edits. The issue to ABP asking for the filtering is
  still drafted, not filed.
- **Usage summed from the descriptors on every store**, the soft limit of the allocation, the directory tree's id paths,
  the signed tickets and links, pending files, rate limits: as [`peer-comparison.md`](peer-comparison.md) and
  [`business-system-integration-decision.md`](business-system-integration-decision.md) left them.

## Not verified

- Rolling a save back under a real transaction. The test databases run without them, so the tests make a unit of work
  count as transactional (`RollingBackFileDescriptorManager`) and check the BLOB; the row's own rollback is the
  database's.
- Two overriding saves without the name lock settled by the concurrency stamp; nothing exercises it.
- That an integration service is absent when the host does not expose them. The test host exposes them and cannot run a
  second one; the API definition marks both controllers as integration services.
- The upgrade of an existing MongoDB database: the directory collection's `NameKey` index lost its partial filter, and
  one that was created before cannot be rebuilt under the same name; the Migrate note says to drop it.
- The Angular pages were not driven in a browser, only built and tested.

## Replaced in older records

[`blob-management-decision.md`](blob-management-decision.md), [`positioning-decision.md`](positioning-decision.md),
[`business-system-integration-decision.md`](business-system-integration-decision.md) and
[`upload-checksum-decision.md`](upload-checksum-decision.md) carry a note at their top for what they say that is no
longer so: soft deletion, the name key handed between rows, the file API's two callers and its path, and the two
paths of the BLOB and file APIs.

## Amendment 2026-11-23: a file's life is its record's

Decided by the owner on 2026-11-23, against the hard delete of the second batch: a business record that refers to a
file may be soft-deleted and restored. If the file went for good when the record was soft-deleted, restoring the record
would bring back a reference to nothing - and the business entity cannot be made to delete for good because the file
did. The second batch was right that the old soft delete protected nothing (it deleted the bytes at once); the wrong
conclusion was to drop recoverability rather than keep the bytes.

What the peers do, read for this amendment:

- Object stores keep deleted bytes recoverable for a time: Google Cloud Storage's soft delete (on by default, 7 days, 7
  to 90), Azure Blob soft delete (1 to 365 days, `Undelete Blob`), S3 versioning's delete markers with lifecycle
  expiration. Supabase Storage has none: a delete is final.
- Systems where records refer to assets keep the two lifecycles apart: Directus will not delete a file still related
  to an item; Drupal counts a file's usages and collects only those no entity uses; Squidex deletes an asset softly
  and for good with `?permanent=true` on its delete endpoints (seen on its support forum, not in its reference).
- ABP's CMS Kit soft-deletes a `MediaDescriptor` and deletes its BLOB at once - the defect this amendment removes
  here, not a model.

Decided:

- **A file kept by id is soft-deleted** (`FileDescriptor` is `IDeletionAuditedObject` again) and keeps its BLOB. Hidden
  from every read - listings, by id, the public address, read links - it comes back as it was with
  `IBlobManagementFileService.RestoreAsync(id)`, at the same address; at the top of its container when its directory
  was deleted meanwhile.
- **It is deleted for good only when its business system asks**: `DeleteAsync(id, permanently: true)`
  (`?permanently=true` on the integration API, as Squidex has it), on a live file or a soft-deleted one - the row now,
  the BLOB after commit. No retention period, and no cleanup by the module: the owner's call, since only the business
  system knows when its record is gone. The one exception is a file uploaded with a ticket and never confirmed, which
  no record refers to: the pending cleanup still deletes it for good, and a pending file is always deleted for good.
- **A file saved under a name is deleted for good at once.** ABP's `IBlobContainer` has neither a restore nor a delete
  for good, so a soft-deleted one would hold its name for ever. Soft deletion therefore only ever touches files kept by
  id, which have no name key: nothing is handed between rows.
- **The allocation counts soft-deleted files**: their bytes are stored. The usage of the tenant shows that part apart
  (`DeletedBytes`, `DeletedFileCount`).
- **A ticket stores one file**, also while that file is soft-deleted: the replay check reads soft-deleted rows again.
- **A directory is soft-deleted** (`FullAuditedAggregateRoot`, as ABP's `OrganizationUnit`), and **has no name key**.
  Sibling names are checked in `DirectoryManager` against the siblings read from the database, as written, as ABP
  checks an organization unit's display name among its siblings (`OrganizationUnitManager.ValidateOrganizationUnitAsync`).
  Two simultaneous creates can leave two siblings of one name; a directory is known by its id, so that costs a
  duplicate label, not a wrong file. A soft-deleted file does not keep its directory from being deleted.
- **A file's name key stays**, weighed again with the owner: it is what makes the lookup of a name exact on every
  database (the name column compares as the collation does, case-insensitively on SQL Server and MySQL by default), and
  what keeps an overriding save from finding - and deleting the bytes of - another file.

Costs: soft-deleted files and their bytes stay until their business systems delete them for good, and count against
the tenant's allocation until then; a business system that never deletes for good keeps them for good, as it keeps
its records. A project that ran a pre-release adds a migration that puts back `IsDeleted`, `DeleterId` and
`DeletionTime` on both tables and drops the directory's `NameKey` (`CHANGELOG.md`).

