# Decision: Drive is a per-tenant cloud drive with two tiers; it partly supersedes core-only

**Status:** accepted, 2026-11-10 · **Applies from:** the first release that contains `Dignite.Drive.*` ·
**Supersedes in part:** [`core-only-decision.md`](../../blob-storing/dev-docs/core-only-decision.md) (its reasons 1, 2 and 4)

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

Two entry points stay separate inside the module. The **integration entry** (from the provider) authenticates
*callers* — clients and tickets, with in-process callers trusted — and tenants, never users, and works on *system*
containers that are not shown in the UI. The **interaction entry** (from people) authenticates users against the
Drive's own permissions and works on *user drives*. Merging them would bring back the permission-bridging problem
described below.

## What `core-only-decision.md` argued, and where it stands

| # | Reason then | Now |
|---|---|---|
| 1 | A module with an HTTP API cannot be shared by two services behind one gateway | **Settled.** Applications get no Drive API to install; they install a provider. There is one Drive installation per deployment environment: one host installs Application and EF Core, or the Drive runs once as a service — never two hosts each installing it behind one gateway. |
| 2 | The file browser is a product feature of Site, not platform infrastructure | **Retired.** More than one product needs a file library, and a cloud drive is a product in its own right. Site retires its own library in two steps: first swap the provider (`UseFileSystem` to `UseDrive`), then move the picker, the CKEditor upload and its file tools to the Drive's file API. Two file libraries in one product is the over-design to avoid. |
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

## Still open

Owner decisions the plan lists that this record does not settle yet:

- Whether an application may promote a system container into the Drive's UI.
- Where the flex-fields file field type lives (registration key `FileExplorer`).
- What becomes of Site's `site_*` file MCP tools once the Drive has its own.
- Which tunables become ABP Settings in v1 (the plan: none).

Record each here when it is decided.
