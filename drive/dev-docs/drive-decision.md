# Decision: Drive is a per-tenant cloud drive with two tiers; it partly supersedes core-only

**Status:** accepted, 2026-11-10 · **Applies from:** the first release that contains `Dignite.Drive.*` ·
**Supersedes in part:** [`core-only-decision.md`](../../blob-storing/dev-docs/core-only-decision.md) (its reasons 1, 2 and 4) ·
**Amended 2026-11-11:** the Drive splits into an open edition and Drive Pro, see
[the amendment below](#amendment-2026-11-11-the-file-library-leaves-for-drive-pro)

The plan, the design and the phases live in [issue #379](https://github.com/dignite-projects/abp-modules/issues/379).
This record keeps the decisions and the reasons for them, so they outlive the issue.

## Decision

A new tree, `drive/`, ships two kinds of packages:

- `Dignite.Abp.BlobStoring.Drive` — an ABP `IBlobProvider`, and the only package a business module references:
  `container.UseDrive(...)` makes any of the application's own blob containers keep its bytes in the Drive, after the
  application's own pipeline contributors have run and under the application's own authorization.
- `Dignite.Drive.*` — a per-tenant cloud drive: the storage the provider talks to (blob API, tenant quotas, content
  dedup, public read with image sizing, upload and download tickets) **and** a user-facing file library on the same
  descriptor table (directories, a file picker, the Drive's own permissions and per-item resource permissions).
  Hostable in one business host (in-process) or once as its own service.

Amended 2026-11-11: the user-facing file library described here is to be divided — attaching a file stays in the open
Drive, the library itself becomes Drive Pro; see
[the amendment below](#amendment-2026-11-11-the-file-library-leaves-for-drive-pro).

Two entry points stay separate inside the module. The **integration entry** (from the provider) authenticates
*callers* — clients and tickets, with in-process callers trusted — and tenants, never users, and works on *system*
containers that are not shown in the UI. The **interaction entry** (from people) authenticates users against the
Drive's own permissions and works on *user drives*. Merging them would bring back the permission-bridging problem
described below.

## What `core-only-decision.md` argued, and where it stands

| # | Reason then | Now |
|---|---|---|
| 1 | A module with an HTTP API cannot be shared by two services behind one gateway | **Settled.** Applications get no Drive API to install; they install a provider. There is one Drive installation per deployment environment: one host installs Application and EF Core, or the Drive runs once as a service — never two hosts each installing it behind one gateway. |
| 2 | The file browser is a product feature of Site, not platform infrastructure | **Retired.** More than one product needs a file library, and a cloud drive is a product in its own right. Site retires its own library in two steps: first swap the provider (`UseFileSystem` to `UseDrive`), then move the picker, the CKEditor upload and its file tools to the Drive's file API. Two file libraries in one product is the over-design to avoid. **Amended 2026-11-11:** Site still gives up its own library — `SiteFileDescriptors` goes once Site references the open Drive. What replaces it is divided: attaching files comes from the open Drive, the library from Drive Pro where the host installs it. |
| 3 | What the platform shares is blob storage, and that is a library, not a service | **Unchanged.** ABP BlobStoring stays the shared layer and the Drive is one more provider for it. `blob-storing/` keeps only pipeline contributors and gains no upload entry point, metadata, API or permissions. |
| 4 | Each service owns its file metadata and its authorization | **Settled by the two tiers.** An application keeps its own metadata, pipeline rules and user authorization and hands only bytes to the Drive. The Drive never evaluates another service's rules; its own file API uses its own permissions. |

The first version of the plan was one file-library module that bridged Drive operations to business permission names
through container configuration. That left reason 4 unresolved for entity-shaped rules and turned policy names into a
cross-service contract. The two-tier split replaced it, and the bridging stays rejected: "whoever may create content
may upload" is role administration (grant the role the Drive's permissions as well), which is the normal way one
role holds permissions of several services in an ABP microservice solution.

## Consequences for the repository

- **Repository invariant 2 is replaced, not deleted** ([`CLAUDE.md`](../../CLAUDE.md)): trees may `ProjectReference`
  each other as long as the graph is acyclic and each tree's `CLAUDE.md` names what it depends on. `drive/` depends on
  `blob-storing/`, whose contributors run on both tiers; `blob-storing/`'s "what stays out" is unchanged. `dotnet pack`
  turns the reference into a same-version `PackageReference` (lockstep release, as in ABP's own monorepo). The
  infrastructure trees' one-way rules stay.
- **Names**, confirmed 2026-11-10: module `Dignite.Drive.*` (no `Abp` infix: an application module with a domain model
  and a UI, like `Dignite.NotificationCenter`); provider `Dignite.Abp.BlobStoring.Drive`; npm `@dignite/ng.drive`;
  permission group and remote service name `Drive`; route prefix `api/drive`; MCP tool prefix `drive_`; table prefix
  `Drive`; docs in `docs/en/drive/`; label `module: drive`. They were free on nuget.org and npm on 2026-11-10; check
  again before the first publish. The `Dignite.FileExplorer.*` IDs stay retired and are never reused.
- **Nothing is published until Drive is complete**, decided 2026-11-10. `drive/Directory.Build.props` makes every
  Drive package non-packable unless `-p:PackDrive=true` is passed, so a release packs and pushes none of them; CI passes
  it to keep verifying how they pack. To publish: delete that property group and the opt-in in `ci.yml`, add the drive
  smoke test back to `release.yml`, wire `@dignite/ng.drive` into the npm publish step (it needs a Trusted Publisher on
  npmjs.com first and a place in `verify-npm-single-copy.mjs`), and check the names again.
- **The blob API authorizes clients with an allowed-client list on each system container's configuration**, decided
  2026-11-10. It needs no permission data on the blob path, so a Drive running as its own service needs neither the
  OpenIddict tables nor shared grant tables for it. Rules that come with it:
  - The caller must be a client, not a user. A token that carries a user id is rejected: user tokens carry `client_id`
    too, so an allow-listed first-party client would otherwise let its users write (browsers go through tickets).
  - The check sits behind an interface (`IDriveClientAuthorizer`), so `C`-provider resource grants can be added later as
    a second source, allowed when either source allows.
  - An empty list allows no remote caller (in-process callers are trusted), and a system container with an empty list
    is a startup warning. An unknown client id is a startup warning only where the host can look clients up: ABP's
    `IApplicationFinder` lives in `Volo.Abp.OpenIddict.Domain`, so a host that has it (a Drive installed next to its
    authorization server) checks the ids, and a standalone Drive service skips the check and logs that it did
    (amended 2026-11-10; the first wording promised the warning everywhere).
  - One list per container covers read, write and delete; split it when a read-only client exists.
  - Revoking an application stays the authorization server's job (disable the client); the list only limits which
    containers a client may touch.

  Why not `C` grants: ABP registers the management side for `C` resource grants
  (`ApplicationResourcePermissionManagementProvider`) only where `IApplicationFinder` exists, which is where
  `Volo.Abp.OpenIddict.Domain` and its tables are, not in a standalone Drive service. "Manage at runtime in ABP's
  dialog" would therefore mean managing them in the authorization server against a shared grants table, every blob call
  would read the permission store, and system containers would have to be modeled as permission resources. Revisit it
  if applications are onboarded often or by third parties.
- **Usage is metered per tenant only**, decided 2026-11-10: one Feature, `Drive.StorageSize`, and no per-container
  usage accounting in v1.
- **A descriptor is addressed by the caller's name; the physical blob is named by the Drive**, decided 2026-11-10. On
  the provider path the application names its blobs and reads them back by those names (`IBlobProvider`), and
  `SaveAsync` returns nothing, so a name the Drive chose could never reach the application. So a `FileDescriptor`'s
  `Name` is the caller's blob name — in a system container its address, unique among the live descriptors of its
  tenant and container — and its `BlobName` is the physical blob in the Drive's storage, generated by the Drive (32 hex
  digits plus the canonical extension of the detected type) and shared by every descriptor that deduplicates to it.
- **The dedup arbiter is a unique key column, not a filtered index**, decided 2026-11-10. "Unique over live rows" needs
  an index that skips deleted rows; a filtered index is SQL Server / SQLite / PostgreSQL syntax (each its own) and MySQL
  has none, while a reusable module has to run on every EF Core provider. So the descriptor that wrote a blob carries a
  nullable `DedupKey` (tenant, container, hash) while it is live, cleared when it is deleted, under a plain unique index:
  every provider treats `NULL`s as distinct there (EF Core adds the `IS NOT NULL` filter on SQL Server itself), and
  MongoDB gets a partial unique index on the field's presence.
- **Storage operations keep descriptor and bytes consistent with a dedup lock, a delete after commit and a name key**,
  decided 2026-11-10. Three gaps were open in `FileDescriptorManager`. A delete removed the physical blob inside the
  unit of work, so a rollback brought the row back without its bytes. An upload that found a dedup source could insert
  its sharing row while the source's last descriptor was being deleted, and the delete, not seeing that row, removed the
  blob. And a system container's name was unique only by check-then-act, under a non-unique index. Now an upload takes
  an `IAbpDistributedLock` named by the dedup key before the lookup and holds it until its unit of work is disposed (a
  unit of work that holds it already reuses it, kept in `IUnitOfWork.Items`); when the lock cannot be had within a
  bounded wait it stores the bytes as a fresh blob without claiming the dedup key, rather than failing. A delete
  soft-deletes the row in the unit of work and removes the blob in `OnCompleted`, after commit, under the same lock and
  only when a fresh query finds no live descriptor naming it; that step is best-effort, since an orphaned blob is
  unreachable bytes, never lost data. Because the uploader holds the lock until its row is committed or rolled back,
  the deleter's check sees that row or its absence. The unique `DedupKey` stays the last-resort arbiter. A name gets the
  same treatment as content: live descriptors of system containers carry a nullable `NameKey` (SHA-256 of tenant,
  container and name — a hash, since unique-index keys are size-limited) under a plain unique index, and an override
  releases the old descriptor's key before it stores the new one. ABP's default lock is in-process, so several Drive
  instances need a distributed lock provider, as tickets already did. Rejected: committing each storage operation in
  its own unit of work — nested transactional units of work are not portable across providers or the tests' SQLite;
  and locking the source row in the database — it needs transactions, which a MongoDB host may not have.
- **The BLOB API takes the BLOB name in the query string and has no `HEAD`**, decided 2026-11-10. The plan had
  `api/drive/blobs/{container}/{name}` with `PUT`/`GET`/`HEAD`/`DELETE`. The routes are
  `api/drive/blobs/{containerName}?blobName=…` (`PUT`, `GET`, `DELETE`) and `GET …/{containerName}/exists?blobName=…`.
  A BLOB name may be a path (`2026/11/photo.png`): as a route value, ABP's generated C# proxies encode its `/` and
  ASP.NET Core's catch-all routes do not reliably decode `%2F`, while a query value means the same on both sides. ABP's
  static proxies do not generate `HEAD` calls, so existence is its own `GET` — the plan listed an existence check anyway.
  The remote provider is the API's only intended caller, so the shape follows what its proxies can express.
- **A public read names its tenant with `?__tenant=`**, decided 2026-11-10. A public read is anonymous, and a BLOB name
  is unique only within its tenant, so the address has to say whose file it is. `IDrivePublicUrlBuilder` appends
  `__tenant={id}` when the file belongs to a tenant, read by ABP's query-string tenant resolver (on by default); a host
  that resolves tenants by domain serves the address without it too. The route stays
  `api/drive/public/{container}/{blob}`. Not like Site, whose addresses name no tenant: a Drive run as its own service
  usually cannot tell a tenant from its own domain. Rejected: the tenant in the route (more robust against a host's
  resolver setup, but it overrides the tenant ABP resolved and departs from the planned route). The endpoint removes
  `Set-Cookie`, so the tenant a query string resolved is never remembered in a shared cache.
- **Tickets in Phase 1**, decided 2026-11-10 with the Phase 1 plan. The plan had tickets redeemed by the BLOB API and
  carrying the issuer's claims, re-checked at redemption. On the integration entry the issuer is an application, so a
  ticket names its client id (none for a trusted in-process issuer) and redemption checks that the client is still
  allowed in the container; the directory a ticket may name arrives with directories in Phase 2. Tickets are redeemed at
  their own addresses, `POST api/drive/tickets/upload?ticket=` (form field `content`) and
  `GET api/drive/tickets/download?ticket=`, not the BLOB API: those are anonymous browser addresses without
  antiforgery, while the BLOB API admits clients only. The ticket names its tenant, so the addresses carry no
  `__tenant`. An upload ticket is good for one *successful* upload (a refused one can be retried until it expires); a
  download ticket is good until it expires (10 minutes by default, at most an hour), since a browser may fetch a file
  more than once.
- **User drives**, decided 2026-11-10 with the Phase 2 plan:
  - **A name is not an address in a user drive.** A file there is addressed by its id, and two files may share a name,
    even in one directory, as in Site's library. Only system containers keep names unique among live descriptors,
    because there the name is what the application reads the file back by. Directory names stay unique among their
    siblings, since a tree with two folders of one name cannot be navigated.
  - **A user-drive file's public address names its physical BLOB.** In a `PublicRead` user drive,
    `api/drive/public/{container}/{blob}` takes the `BlobName` (32 random hexadecimal digits, not guessable), because
    a name is not unique there. System containers keep the caller's name in that place.
  - **Module-wide permissions stop at other people's personal drives.** In a tenant-wide drive (`Scope = Tenant`)
    the `Drive` permissions act on every item. In a personal drive (`Scope = Personal`) they act only on the user's own
    items; another user's items are reached through a resource grant. Rejected: an administrator who sees every
    personal drive through a module-wide permission. A holder of `Drive.ManagePermissions` can still grant
    themselves access, which makes that an administrator's permission.
  - **The file API serves users only.** A token without a user id is refused, mirroring the BLOB API, which refuses
    tokens with one. Applications store bytes through the provider.
  - **Granting is an administrator's tool in Phase 2.** ABP's resource-permission dialog checks the management
    permission as a standard permission (`PermissionAppService` checks `ManagementPermissionName` with
    `IsGrantedAnyAsync`), not per resource, so whoever holds `Drive.ManagePermissions` manages grants on every item and
    an owner cannot share their own folder through it. Owners sharing their own items need an endpoint of the Drive's
    own; it comes with share links in Phase 3.
  - **A personal drive is modeled by `OwnerId`**, on `DirectoryDescriptor` and `FileDescriptor`: the user whose
    personal drive holds the item, `null` in a tenant-wide drive and in system containers. A directory tree is
    (tenant, container, `OwnerId`). An item placed in a directory takes the directory's owner, whoever put it there,
    so the owner of a personal drive keeps everything in it. Not `CreatorId`: someone granted `Add` on another user's
    folder creates items that belong to that user's drive.
  - **The Drive works out access itself; ownership is not a resource-permission value provider**, decided with
    Phase 2 step 2. ABP's `ResourcePermissionChecker` (and its `ResourcePermissionPopulator`) answers whether one
    permission is granted on one resource; it knows neither module-wide permissions, nor directories above an item,
    nor the personal-drive rule. So `DriveAuthorizationService` combines them, and `DriveResourcePermissionHandler`
    gives `IAuthorizationService.IsGrantedAsync(item, permission)` the same answer. The plan's
    `OwnerResourcePermissionValueProvider` was dropped: a value provider gets only a resource key, so it would read the
    item from the database on every check, run for every module's resources, and still not make the checker's answer
    complete. The evaluator already holds the item. The rules:
    - **The personal owner and the creator are two different things** (amended 2026-11-10). The personal owner - in
      a personal drive, the user whose drive it is - may do anything with every item of it, including what others put
      there. In a tenant-wide drive, which has no owners, the creator of an item may view, rename, move and delete it,
      and upload and create subdirectories directly in a directory they created; nothing more. Being the creator of a
      directory gives no access to what others put below it, and the walk up the tree counts only `View` and `Add`
      grants, not who created an ancestor. The first wording made the creator the owner of a tenant-wide item, with the
      owner's reach below a directory: whoever created a top-level folder then saw everything anyone ever filed under
      it, and kept seeing it after every Drive permission they had was revoked - a lasting right that nobody granted
      and nobody could take away short of moving the folder. Seeing everything in a tenant-wide folder now takes
      `Drive.Directories` or a `View` grant on it or above, which an administrator gives and revokes. So a directory's
      access tells opening it (`View`, which its creator has) from seeing everything in it (`ViewContents`), and a
      creator who may only open their folder is listed their own files and subdirectories in it.
    - Module-wide permissions and a container's grants (`Drive.Container.Upload`, `Drive.Container.CreateDirectory`)
      reach every item of a tenant-wide drive and the user's own items in a personal drive. A container grant is the
      module-wide permission limited to one container.
    - A grant on an item applies to that item. A directory's `View` and `Add` grants also reach everything below it.
    - The top of a drive is no one's item, so putting things there needs a module-wide permission or a container grant.
      An owner alone cannot do it.
    - Uploading or creating a directory is allowed when any of these allows it. The plan's "the container resource plus
      the directory's `Add`" is read as alternatives, not as both together: requiring the container grant as well would
      keep a user who was given `Add` on someone's personal folder out of it.
  - **Directories have `Update` and `Delete` resource permissions too** (`Drive.Directory.Update`,
    `Drive.Directory.Delete`), beside the plan's `View` and `Add`. Files had all three of theirs, and without these a
    folder could be renamed or deleted only by its owner or a module-wide permission holder.
  - **Grant cleanup without requiring Permission Management**, decided with Phase 2 step 3. The plan had
    `Dignite.Drive.Application` depend on `Volo.Abp.PermissionManagement.Domain`. It references the package, for
    `IResourcePermissionGrantRepository`, but does not depend on its module: a Drive service that only serves
    applications needs no permission data (see the allow-list above), so it must start without Permission Management's
    tables. `DriveResourcePermissionCleaner` deletes a file's or directory's grants in the deleting unit of work where
    the host has the grant store, and does nothing where it has none.
  - **What the file library lists**, decided with Phase 2 step 3:
    - Inside a directory the caller may see everything in, everything in it, since a view reaches everything below;
      inside one they may only open as its creator in a tenant-wide drive, their own files and the subdirectories they
      may view (amended 2026-11-10 with the creator rule above; the first wording had no directory one could open
      without seeing all of it).
    - At the top of a personal drive, the caller's own items.
    - At the top of a tenant-wide drive, everyone's files to holders of `Drive.Files`, otherwise the caller's own
      uploads. For directories there, those the caller may view.
    - Directories are listed one level at a time (`ParentId`), with `HasChildren`, not as the whole tree at once as
      Site's library returns them. A tenant-wide tree can be large, and every node would need its own access check.
    - Items granted from another user's drive are reached by opening the granted directory. A "shared with me" list
      across drives comes with owner sharing in Phase 3.
  - **A directory's DTO says both what is granted and what can be done.** `ResourcePermissions` holds the
    `Drive.Directory.*` answers that `IAuthorizationService` gives, where `Add` means adding both files and
    directories. `CanUpload` and `CanCreateDirectory` say which of the two the caller may do, because a module-wide
    permission or a container grant can allow one without the other.
  - **A user's ticket into a user drive is its own kind**, decided with Phase 2 step 4.
    `IDriveFileAppService.CreateUploadTicketAsync` issues a `FileUpload` ticket. Issuing is authorized exactly as the
    upload would be, so a ticket is never issued for an upload that would be refused. The ticket names the drive, the
    directory, the file's name and the issuing user: id, user name, and the roles the user had when issuing it. It is
    redeemed at `POST api/drive/tickets/upload-file?ticket=` and answers with the file's DTO. Redemption runs the file
    library's own upload as that user, so the drive, the directory and every permission are checked again at that
    moment. A permission revoked in between makes the ticket invalid (`Drive:InvalidTicket`), and a refused attempt
    does not use the ticket up. It is not the system upload ticket with a user added: that one's issuer is a client
    checked against an allow-list and names the file by its address, and keeping the kinds apart means neither can be
    redeemed at the other's address. Only uploads get a user ticket in Phase 2; reading a private file is the file
    API's download.
  - **The `drive_*` MCP tools**, decided with Phase 2 step 6.
    - What they cover: list the drives; list and create directories; list, get, upload, rename or move, and delete
      files; get an upload link.
    - Renaming, moving and deleting directories stay in the Drive's UI, as Site's tools leave them. A directory carries
      its files with it, which is a larger change than an assistant should make in passing.
    - Each tool calls the file library's application services and repeats none of their rules.
    - The upload link is the user's `FileUpload` ticket. Its command is `curl -F "content=@FILE_PATH"` (`curl.exe` in
      PowerShell), because ABP binds an uploaded stream from a form only, unlike Site's `PUT` of the raw body.
    - The link's address comes from `DriveMcpOptions.BaseUrl`, or else from the address the MCP request came in on.
      Set it when the Drive runs as its own service.
    - `drive_upload_file` takes base64 up to `DriveMcpOptions.MaxInlineUploadSize` (2 MB by default, which fits the MCP
      endpoint's default 4 MB body). Unlike Site, it does not raise the endpoint's limit.
    - The tests are a project of their own, `Dignite.Drive.Mcp.Tests` (Site's layout): the registration in the
      `drive` namespace, plus the tools against substituted services.
  - **The name-addressed operations serve system containers only.** `FileDescriptorManager.CreateAsync` (by name),
    `FindAsync` and the BLOB API refuse a user drive as an unknown container, so the integration entry cannot reach
    a user drive.
  - **A directory carries its path of ids**, decided 2026-11-10. Permissions on a directory reach everything below
    it, so every check needs the directory's ancestors; reading them parent by parent cost one query per level, and
    Phase 3 (trash, recursive operations) needs whole subtrees. `DirectoryDescriptor.Path` holds the ids from the top
    of the tree down to the directory itself (`Guid.ToString("N")`, joined by `/`): the authorization takes the
    ancestors' ids from it and reads no directory to walk up, `DirectoryManager.GetPathAsync` reads them in one query,
    and a subtree is a prefix query on an indexed column. A move rewrites the path of every directory below the moved
    one, deleted ones included, since the trash will restore them under it. Not ABP's `OrganizationUnit.Code`, which
    solves the same problem with per-level counters: a counter is assigned check-then-act, so two simultaneous
    creates can get the same code, and a soft-deleted unit's code is reused, which a restore would collide with. An
    id path is unique by construction. A tree is at most 16 levels deep (`DirectoryDescriptorConsts.MaxDepth`, which
    bounds the column at 527 characters), checked on create and, for the deepest directory carried, on move
    (`Drive:DirectoryTooDeep`). There is no backfill of existing rows: the Drive is unpublished and has no users.
  - **The file library is part of the tenant's Drive feature**, decided 2026-11-10. The container, directory and file
    services require `Drive.Enable` (`[RequiresFeature]`), so a tenant whose Drive is off gets no file library at all,
    rather than one that lists and reads but refuses uploads. Public reads are deliberately not gated: what a tenant
    published stays at its address, and storing is already refused by `DriveUsageManager`. Ticket redemption and the
    BLOB API are unchanged; a user's upload link redeemed while the Drive is off is refused like any other upload the
    issuer may no longer make.
  - **A public read is resized only to the sizes the host allows** (`DrivePublicReadOptions.AllowedSizes`, a default
    ladder), decided 2026-11-10; any other `Width`/`Height` is a 400. An anonymous address that renders any of 16
    million size combinations cannot be cached usefully and invites making the Drive render them; a short list bounds
    what a shared cache has to hold. There is still no server-side cache of renditions: one address requested
    repeatedly is rendered each time unless a cache or rate limit sits in front.
  - **A public read is answered from one lookup of the file**, decided 2026-11-10. The endpoint first asked the
    service for the file's information (`ETag`, `304`, inline or attachment, `HEAD`'s length) and then for its bytes,
    which looked the file up again. In a system container the name is the address, so an overwrite between the two
    made one response carry the old file's `ETag` and disposition with the new file's bytes and type: a shared cache
    kept the new bytes under the old validator for a day, and "a type outside the Drive's list is never rendered
    inline" was decided on a different file than the one served. `IDrivePublicAppService.ReadAsync` now takes the
    client's entity tags and whether only headers are wanted, finds the descriptor once, and returns its information,
    its entity tag, the not-modified decision and - only when wanted - the bytes of that same descriptor. One query per
    read instead of two.

## Amendment, 2026-11-11: the file library leaves for Drive Pro

The owner decided the direction on 2026-11-11. When this section was written no code had moved; the cut below was
made the same day, and `drive/` now holds the open edition only. "Decided" below is what the owner said, kept close to his words; "Proposed" is how it could be built and is
not confirmed. An earlier draft of this section, written the same day, recorded three inferences as decisions (that
Site keeps its own file library, that Drive Pro has file tables of its own, that the open Drive keeps directories); none
of them was the owner's, and they are corrected here.

### Decided

- **The Drive splits into an open edition and Drive Pro.** This repository keeps the open Drive: the storage tier (the
  provider, descriptors, content dedup, the tenant allocation, public reads with sizing, tickets, the BLOB API) and what
  an application needs to attach a file — upload one, read one, delete one. **Dignite Drive Pro**, a commercial product
  in a repository of its own for commercial ABP modules, is the drive in the full sense: names that can be changed, the
  directory tree, listing by container and directory, rename, move, and from Phase 3 the trash and share links. Why:
  that is a cloud-drive product, which is more than this repository's modules are for.
- **Site and flex-fields are open source and never depend on Drive Pro.** Site references the open Drive only.
- **Once Site references the open Drive, its own `SiteFileDescriptors` table goes.** Site does not keep a file library
  of its own beside the Drive.
- **Site's code is the same whichever edition the host installs.** Drive Pro replaces the open Drive's components and
  services of the same name in the host; a host with only the open Drive gives Site upload-and-attach, a host with
  Drive Pro gives it the library. That Drive Pro can take the place of the open components without Site or flex-fields
  changing is the aim.
- **Reads.** With the open Drive, Site's files need no authorization and are output directly. Drive Pro, as a drive,
  normally authorizes a read.
- **Site's nine `site_*` file tools are divided between the open Drive and Drive Pro**, not dropped: an integrated Site
  still has them.
- **In a host with only the open Drive, Site does not show every file that was uploaded, but a file it currently shows
  can be deleted.**
- **When Site is deployed in a host that has Drive Pro, migrating its existing files loses nothing.**
- **Considered and dropped, 2026-11-11: building Drive Pro on ABP's commercial File Management module.** It is a file
  library on BLOB storing with directories, per-item resource permissions, share links and download tokens. By its
  documentation it stores through any BLOB provider, so it should run on `UseDrive`; that was not tested. It needs an
  ABP Team licence or higher, keeps everything in one container, and has no personal drives, no upload tickets for
  assistants and no MCP tools. The owner dropped the idea. It is recorded because Phase 2 was designed without
  comparing against it.
- **Which operation is on which side.** Open Drive: upload a file (form upload and an upload link), read one file's
  details, read its bytes, delete one file, and list the containers a caller may upload to with their limits. Drive Pro:
  list the files of a container or directory, directories (list, create, rename, move, delete), rename and move a file,
  the Drive's permissions and per-item grants, personal drives, user-facing drive pages. For Site's tools that gives —
  open Drive: `site_upload_file`, `site_create_upload_url`, `site_get_file`, `site_delete_file`,
  `site_list_file_containers`; Drive Pro: `site_list_files`, `site_list_directories`, `site_create_directory`,
  `site_update_file`. Their new names are in the MCP item below. Proposed in this record and confirmed by the owner
  2026-11-11.
- **One file table, in the open Drive; Drive Pro builds on it.** A file Site stores through the open Drive has to be the
  same file Drive Pro's library shows once the host installs it, so Drive Pro does not keep a parallel file table. A
  file attached through the open Drive is a row addressed by its id, with the name it was uploaded under (a file an
  application stores through the provider is still addressed by the name it gave); what Drive Pro adds to a file (the
  directory it is filed in, the owner of a personal drive, later a trash state) it adds to that row, and
  `DirectoryDescriptor` is Drive Pro's own table. How it adds them, in order of preference: ABP's object extensions
  mapped to columns (`MapEfCoreProperty`), which the owner suggested and which keep the open entity free of library
  fields — to be proven on MongoDB first, where extra properties are stored and queried differently; else typed
  nullable columns on the open entity that the open edition leaves unused. Rejected: a side table keyed by the file's id
  (every listing becomes a join across two modules' tables, and a file never filed has no row in it); a separate Drive
  Pro file table (the earlier draft's proposal — files uploaded before Drive Pro, or through the open components, would
  not be in the library). Confirmed by the owner 2026-11-11, with object extensions as the way to add the fields. The
  MongoDB proof was done the same day (below), so the fallback of typed nullable columns is not needed.

The items below were proposed in a planning session on 2026-11-11, each with its reason and cost, and the owner accepted
them as a set ("all as recommended"). The wording is the proposal's, not his.

- **The open Drive keeps two kinds of container.** One an application stores into through the provider (addressed by
  name, authorized by client, as in Phase 1), and one users attach files to through the file API (addressed by id,
  keeping the name the file was uploaded under; `UserDrive` today). `Scope` (tenant-wide or personal) goes to Drive Pro
  with personal drives.
- **Reads in the open Drive.** A `PublicRead` container is read anonymously. In a container that is not `PublicRead`,
  reading a file by its id needs a signed-in user and nothing finer. Cost: any signed-in user of the tenant who has the
  id can read the file; an application with private files stores them through the provider or hands out download
  tickets.
- **The seams Drive Pro replaces.** C#: `IDriveFileAppService` - in the open Drive upload, create an upload link, read
  the details, read the bytes, delete - and `IDriveContainerAppService` (the containers with their limits). Drive Pro
  replaces their implementations (`ReplaceServices`); listing, rename, move and directories are on Drive Pro's own
  interfaces. The open `DriveFileDto` and `DriveContainerDto` become extensible objects, so what Drive Pro adds to a
  file travels in `extraProperties`; they carry no `DirectoryId`, `OwnerId` or `ResourcePermissions`. Angular: one
  component key, `Drive.FileInputComponent`, for a component the open Drive does not have yet (its value is a list of
  file objects, its inputs the container name and whether several files are allowed; it uploads, shows and removes),
  which Drive Pro replaces with one that also picks from the library. `Drive.DriveComponent`, the page, goes to Drive
  Pro, as does `drive-file-picker`, which is built on listing. A CKEditor upload needs no Angular seam: it calls the
  upload API, which is replaced on the server.
- **Who may upload and delete in the open Drive.** The module-wide permissions `Drive.Files.Upload` and
  `Drive.Files.Delete`, and the uploader may delete their own file (`CreatorId`). No rule per container. Why: a browser
  uploads straight to the Drive's API, so "the application authorizes first" means either the application proxying the
  upload (which Drive Pro could then not replace) or an application hook running in the Drive's process (which fails
  when the Drive is its own service and brings bridging to business permissions back). Cost: Site loses its mapping of
  containers to business permissions (`theme-assets` needs `SiteAdmin.Themes.Edit` today), and an administrator grants
  the Drive's permissions to the roles that edit content.
- **MCP: both editions contribute to the one `drive` module.** Calling `AddAbpMcpModule("drive", …)` again extends the
  module, which is what the shared server offers a downstream package; a `drive_pro` module would be refused as nested
  in `drive`. Site's nine file tools become the nine `drive_*` tools that exist, and Site contributes no file tools of
  its own - open Drive: `drive_upload_file`, `drive_create_upload_url`, `drive_get_file`, `drive_delete_file`,
  `drive_list_containers`; Drive Pro: `drive_list_files`, `drive_list_directories`, `drive_create_directory`,
  `drive_update_file`. The server refuses a tool name registered twice, so the open tools take no directory and Drive
  Pro cannot register `drive_upload_file` again; how Drive Pro uploads into a directory is its own design.
- **Drive Pro's names.** Package ids `Dignite.Drive.Pro.*`. The route prefix `api/drive`, the remote service name
  `Drive`, the permission group `Drive`, the table prefix and the connection string name `Drive` are the open Drive's,
  and Drive Pro adds to them; it has to be the same database, since its columns are on the open Drive's table. This
  follows ABP's Identity and Identity Pro as remembered, not checked against the commercial source. The npm name is
  left open, to be decided with how Drive Pro is distributed (a private npm feed is configured per scope).
- **The file field type stays in Site.** `FileFieldType` stays in `Dignite.FlexFields.Site` and its control in
  `@dignite/ng.site`, under the key `FileExplorer`; the control renders the Drive's `Drive.FileInputComponent` instead
  of Site's picker, so a host with Drive Pro gets the library picker without Site changing. Not moved into `drive/` or
  `flex-fields/`: that would be a new edge between two trees of this repository for one consumer.
- **Site's theme tooling does not need to list `theme-assets`.** Checked in Site: no theme code calls the file services;
  the container is reached only through the general file tools and the picker. With only the open Drive, upload a file,
  take its address and link it from a template works; seeing what the container holds needs Drive Pro. Known gap, left
  as it is: deleting needs the file's id, and a template stores only its address.
- **Drive Pro's code stays out of this repository's public history.** Phase 2 was never pushed: `main` has Phases 0-1,
  and the file library's commits are on the local branch `feat/drive-file-library` only. That branch and the branch the
  cut is made on stay local; the result is squashed into one commit on a new branch from `main` before anything is
  pushed. The code that leaves is parked as the local tag `drive-pre-split` on `e6302be`, the spike's stash turned into
  a branch, and a git bundle of both kept outside the repository (where: the owner's to name). When the commercial
  repository exists, it is seeded from a clone with the bundle fetched and `git filter-repo` keeping `drive/` and
  `docs/en/drive/`, so the history of Phases 1 and 2 comes along.

### The cut, accepted 2026-11-11

No project leaves as a whole; Drive Pro gets its own `Dignite.Drive.Pro.*` projects later. One step at a time on a local
branch from `feat/drive-file-library`, each built and tested (the four test projects) before the next, with the tests
of what leaves removed in the same step.

0. Park: the tag, the spike branch and the bundle above; create the local branch.
1. MCP: `DriveDirectoryTools`, `drive_list_files`, `drive_update_file` and the directory parameters leave; the five open
   tools stay and the module's instructions are rewritten.
2. The directory API leaves: everything under `Directories/` in the contracts, the application, the HTTP API and the
   client proxies.
3. `IDriveFileAppService` loses `GetListAsync`, `UpdateAsync` and `MoveAsync` with their inputs; the DTOs lose the
   directory, owner and resource-permission members and become extensible.
4. Authorization: `DriveAuthorizationService`, `DriveResourcePermissionHandler`, `DriveResourcePermissionCleaner`,
   `DriveAccess`, `DriveResourceNames` and most of `DrivePermissions` leave; a small one is written for the open rule,
   and `DriveLibraryAppService` folds back into `DriveAppService`.
5. Domain and persistence: `DirectoryDescriptor`, `DirectoryManager`, the directory repositories (EF Core and MongoDB),
   `DriveContainerScope` and `FileDescriptorSorting` leave; `FileDescriptor` loses `DirectoryId`, `OwnerId`, `Rename`
   and `MoveTo`; `FileDescriptorManager` loses moving and personal drives.
6. Migrations of the demo host: the two local Phase 2 migrations go, `Initial` (on `main`) stays, and one new migration
   carries what remains (`NameKey` and its unique index). The open `DriveFileDescriptors` has no `DirectoryId` or
   `OwnerId` column, and there is no `DriveDirectoryDescriptors` table.
7. Regenerate the C# and Angular proxies; narrow `DriveFileApi_Tests`.
8. Angular: the page, `drive-browser`, the directory tree, the file list, `drive-explorer`, the picker, the preview,
   the route and the menu entry leave; the size pipe, the two URL services and the proxies stay.
9. New: the file input component under `Drive.FileInputComponent`.
10. `docs/en/drive/`: `file-library.md` leaves; `permissions.md`, `mcp.md` and `angular-ui.md` are rewritten for the open
    Drive and `docs-nav.json` updated. None of the four was ever published; the five pages on `main` keep their names.
11. Rewrite `drive/CLAUDE.md` (invariants 1 and 3, the project table), the root `CLAUDE.md` and `drive/README.md`.
12. Squash onto `main` on a new branch; push and open the pull request only on the owner's word.

Made 2026-11-11: steps 0-11 one commit each on a local branch from `feat/drive-file-library`, each built and tested,
then squashed onto `main` (step 12), so the public history has the open edition and none of Phase 2's commits.

### Proven by a spike, 2026-11-11

Object extensions carry Drive Pro's per-file fields on both providers, from code outside the open Drive's persistence
projects and with no new seam in it. The spike added a nullable Guid and a string to `FileDescriptor` in test code only
(10 tests on EF Core with SQLite, 13 on MongoDB; the existing 108 of each still passed). Its code is not in the tree.

- **Registration.** In the adding module's `PreConfigureServices`, once (`OneTimeRunner`), before any Drive DbContext
  builds its model: `AddOrUpdateProperty<FileDescriptor, Guid?>(name)`, and for EF Core
  `MapEfCoreProperty<FileDescriptor, Guid?>(name, (entity, _) => entity.HasIndex("TenantId", "ContainerName", name))`.
  The index goes in the property's callback: the entity-level callback runs before the property exists.
- **EF Core.** A shadow column, not repeated in the `ExtraProperties` JSON. It is queried with
  `EF.Property<Guid?>(f, name)` through `IDriveDbContext`; ABP's soft-delete and tenant filters apply as usual; a row
  that never had the value is `NULL`.
- **MongoDB.** The value is a top-level field of the document (ABP maps `ExtraProperties` as extra elements), a Guid as
  binary subtype 4. Query it with the driver's `Mql.Field(f, name, serializer)` inside ABP's queryable, or with a native
  `FilterDefinition` injected into that queryable (`.Inject()`); both keep ABP's data filters. `== null` matches a null
  and a missing field in one query, so files stored before Drive Pro was installed list as unfiled. **Never write
  `f.ExtraProperties[name]` in a query:** it translates without an error to a field that does not exist, finds nothing
  for a value and everything for null. A native filter on the raw collection works too, but gets no data filters.
- **MongoDB index.** ABP has no per-module hook for adding an index to another module's collection. Either replace
  `IMongoModelSource` (run the base, then chain onto the existing index action, which `ConfigureIndexes` would
  otherwise overwrite) or create the index on the collection at startup. Replacing is global and the last registration
  wins; if that becomes a problem, the open Drive can offer a list of index actions on an options class. Not needed now.
- **To design with.** A listing that filters by directory and sorts by name wants `Name` in the index too (MongoDB
  otherwise chose the open Drive's name index). Prefix Drive Pro's property names, so a typed property the open entity
  gains later cannot collide. Not tested: a database per tenant (indexes are created once per process, as the open
  Drive's own are), transactions on the raw collection, SQL Server and PostgreSQL.

### Proposed, to confirm before code moves

These are a way to build what is decided above; the owner has not confirmed them.

- ~~The seams Drive Pro replaces.~~ ~~Who may upload and delete in the open edition.~~ Both confirmed 2026-11-11; see
  "Decided".
- **Migration of Site's files.** Rows of `SiteFileDescriptors` become open Drive rows; in a host with Drive Pro their
  directory placement and Site's directories become Drive Pro's. In a host with only the open Drive there is nowhere to
  keep directory placement. One known difficulty: a Site row that is a dedup reference has an address of its own while
  its bytes are stored under another row's BLOB name, and Site's stored addresses
  (`/api/site-public/files/{container}/{blob}`) must keep working.

## Still open

Owner decisions the plan lists that this record does not settle yet:

- ~~Whether an application may promote a system container into the Drive's UI.~~ Amended 2026-11-11: no longer a
  question for this repository; it goes to Drive Pro.
- ~~Where the flex-fields file field type lives (registration key `FileExplorer`).~~ Settled 2026-11-11: it stays in
  Site; see the amendment.
- ~~What becomes of Site's `site_*` file MCP tools once the Drive has its own.~~ Settled 2026-11-11: divided between the
  open Drive and Drive Pro; see the amendment.
- Which tunables become ABP Settings in v1 (the plan: none).
- ~~Names: Drive Pro's package ids, route prefix, remote service name, table prefix and permission group; the new names
  of Site's file tools; how both editions contribute MCP tools.~~ Settled 2026-11-11; see the amendment. Still open:
  Drive Pro's npm name, with the next item.
- How Drive Pro's packages are distributed: private NuGet and npm feeds, licence checks.
- ~~Where the bundle that parks Drive Pro's code is kept.~~ Settled 2026-11-11: outside every repository, in
  `D:\dignite-projects\_parked\abp-modules-drive-pre-split-2026-11-11.bundle` on the owner's machine. It holds the tag
  `drive-pre-split`, the branch `spike/drive-object-extensions` (the spike's stash as one commit on `90a81a3`) and
  `main`. The tag is on `175c4ea`, the tip of `feat/drive-file-library`, rather than `e6302be`: the one commit between
  them changes only this record, so the parked code is the same and the record parked with it is the accepted plan.
  The commercial repository (`dignite-projects/abp-modules-commercial`) was seeded locally from the bundle the same day,
  `git filter-repo` keeping `drive/` and `docs/en/drive/`; it has not been pushed.
- Confirmation of the migration of Site's files, the one item still under "Proposed, to confirm before code moves".
- ~~The MongoDB proof for Drive Pro's object-extension fields.~~ Done 2026-11-11: it works; see "Proven by a spike".
- ~~Updating `drive/CLAUDE.md` (its invariants 1 and 3 and the project table), the root `CLAUDE.md` and `docs/en/drive/`,
  when the cut is made and not before; the lists of what each tree contains change.~~ Done 2026-11-11 with the cut
  (steps 10 and 11): `file-library.md` gave way to `attaching-files.md`, and `drive/CLAUDE.md` gained an invariant that
  keeps Drive Pro's side of the split out of `drive/`.
- ~~Whether Site's theme tooling needs to list the `theme-assets` container, which the open edition cannot do.~~
  Settled 2026-11-11: it does not; see the amendment.

Record each here when it is decided.
