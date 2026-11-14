# Blob Management: how a business system integrates (2026-11-14)

This record replaces three parts of [`positioning-decision.md`](positioning-decision.md) (2026-11-13): that the module
"has no upload of its own", that tickets are gone (its owner's decision on tickets, the ticket line of *What is removed*
and *Keeping the client tickets* under *Not chosen*), and the container's `PublicRead`. Everything else there stands:
what the module is for, who calls it, usage, directories by grants on the container, no file API for users.

## Decided by the owner, 2026-11-14

- **The module has no business rules.** "User U of tenant T may do this" can only be told to it by a business system
  it trusts. So the credentials come in three layers:
  - **The client token** (OAuth client credentials) stays in the business system's back end. It reaches every tenant
    and is admitted per container. It asks for tickets and links, lists, renames, moves and deletes, and stores and
    reads on the back end's own account.
  - **Upload tickets and read links** are for the browser. Blob Management signs and verifies them; the tenant, the
    container, the file and the uploader are written into them; they live a few minutes.
  - **The public address**: once a business system marks a file public, its address is stable, unsigned and cacheable
    by a CDN.
- **One container of a business system is one managed container.** A business system joins in three steps: the
  authorization server registers its client; Blob Management declares the container and is deployed; the host's
  administrator grants that container's `Files` to the client.

## Admission

`BlobManagement.Container.Files` stays a resource permission granted to the client (provider `C`) on each container -
ABP's own mechanism, which the free edition already covers end to end: `Volo.Abp.PermissionManagement.Domain.OpenIddict`
brings the client provider of the resource permission dialog (`ApplicationResourcePermissionManagementProvider`, and
`ApplicationResourcePermissionProviderKeyLookupService` to find a client, on the host side only) and
`SetForClientAsync` for a seed.

It is now defined on the host side only (`MultiTenancySides.Host`) and no longer restricted to the client provider.
The restriction was a defect: ABP's dialog saves every permission of a resource for whoever it edits
(`PermissionAppService.UpdateResourceAsync`), and `ResourcePermissionManager.SetAsync` throws for a provider a
permission does not list - saving a container's permissions for a user or a role answered 500. A grant to a user or a
role now saves and lets nothing through: `BlobManagementClientChecker` refuses a token that carries a user before any
grant is read. On the host side only, since a client's grant is the deployment's: a tenant's administrator never sees
it, and the check passes because a client-credentials token has no tenant claim, so ABP's checker takes the host side
(`ResourcePermissionChecker`), and the client provider reads its grants on the host (`ClientResourcePermissionValueProvider`).

The tenant of a call is the `__tenant` header ABP's HTTP proxy sends. A granted client can therefore enter any tenant:
that is what a back-end credential is, and the design holds it by keeping the token in the back end.

ABP filters neither the reading nor the saving of a resource's permissions by provider, as it does for ordinary
permissions; an issue asking for it is drafted, not filed.

## A file is public by its business system's mark

`PublicRead` leaves the container's configuration - its key, its configuration check, the container DTO, the page.
Whether anyone may read a file is the business system's call, kept on the file (`FileDescriptor.IsPublic`):

- A BLOB saved under a name takes it from the application's own container (`UseBlobManagement(p => p.IsPublic = true)`),
  which the provider sends with every save.
- A file kept by id takes it from `CreateBlobManagementFileInput.IsPublic`, and changes it with `SetPublicAsync`. A
  file saved under a name is not changed that way: its container decides it at each save.

The public read answers while a live, confirmed file that refers to the address is public, and 404 otherwise. Files
kept by id that hold the same bytes share one physical BLOB and so one address, and it answers while one of them is
public; a file saved under a name has its name as its address and opens no other. A file's `url` is set only while it
is public. The sizes a public read may ask for are the host's default, which a container may override - on ABP's
container configuration, where a container falls back to the default container (`UseBlobManagementPublicSizes`); the
container DTO carries them, so the management page asks only for what the container serves.

Public reads move from `/api/blob-management/public/{container}/{blob}` to `/blobs/{container}/{blob}`: an address a page
embeds and a CDN caches, not an API, so it is neither under `/api` nor named after the module. The query (`__tenant`,
`Width`, `Height`), the `ETag` and `Cache-Control: public` are unchanged. The file API, the upload and the read links
stay under `/api/blob-management`.

## Upload tickets

A business system asks for a ticket (`IBlobManagementFileService.CreateUploadTicketAsync`): the container, the
directory, the user it is for (the remote service sends the application's current user, as for `CreateAsync`), a size
limit, the allowed types and a lifetime capped by `BlobManagementUploadOptions`. Blob Management gives the file's id now
and signs the tenant, container, directory, file id, uploader, client, size, types and expiration with ASP.NET Core
Data Protection's time-limited protector; the keys never leave the module. Precedents: ABP File Management's download
tokens (a short-lived token, bound to a tenant and a file, behind an anonymous endpoint) and Supabase Storage's
`createSignedUploadUrl`.

The browser posts the file to `/api/blob-management/uploads/{ticket}`, anonymously. The tenant is the ticket's; no
cookie or signed-in user is used (the service runs under an anonymous principal); no antiforgery token is asked for; the
body is limited to the ticket's size before it is read; the ticket is kept out of the audit log. The file takes the
ticket's id, so a second use collides with it - refused before anything is stored, or, for two at once, by the primary
key, whose loser's bytes are removed again. The container's pipeline runs as for any file.

The file is **pending** until its business system confirms it (`ConfirmAsync`), having filed it with a record: it is not
listed, gets no read link, is never read publicly, and counts in the usage and the allocation. `GetAsync` shows it with
its uploader, which the business system checks against its current user before it confirms. A file left pending past
`PendingLifetime` (24 hours) is deleted by a background worker, in every tenant ABP's tenant store lists, one instance
at a time under a distributed lock.

## Read links

A business system asks for a link to a file it has decided its user may see (`CreateReadLinkAsync`, or
`CreateReadLinksAsync` for a page of files). The link is signed the same way and names the tenant and the file; its
lifetime is capped by `BlobManagementReadLinkOptions`. The browser reads `/api/blob-management/links/{link}`
anonymously: `Cache-Control: private`, `max-age` no longer than the link has left, and the public read's `ETag`,
`nosniff` and inline-or-attachment rules.

The same file within one window is the same link, so the browser's cache - keyed by the address - serves it again: the
expiration is rounded up to the window (never past the maximum), and the link issued for a file and an expiration is
kept in the distributed cache until then. Data Protection encrypts with a fresh IV, so without the cache every call
would be a new address. A cache that misses only costs a new link.

## Not chosen

- **Ordinary permissions, an allow-list of the module's own, or the user's token passed on** for admission. The first
  cannot be granted per container, the second is a parallel list beside ABP's grants, and the third makes the module
  judge a user it has no rules for.
- **Tickets a business system signs itself.** The keys would leave the module, and every business system would sign
  differently; only the module that verifies a ticket can issue it.
- **A server-side cache of resized pictures**, and **CDN edge verification of signatures**. The first is the CDN's job
  in front of `/blobs`; the second ties the module to one CDN.

## What it costs

- The client token is all-powerful across tenants for the containers it is granted. It must not leave the back end.
- Data Protection's key ring must be persisted and shared by every Blob Management instance, and by nothing else; a
  ticket or link signed by one instance is unreadable to an instance with another key ring.
- A link handed to a browser is a credential for its lifetime; a file deleted meanwhile answers 404, but a link cannot
  be revoked otherwise.
- A pending file takes storage until it is confirmed or cleaned up.
