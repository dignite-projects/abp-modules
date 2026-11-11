# Dignite ABP Modules — monorepo guide

Four **ABP Framework** module trees (`blob-storing/`, `notifications/`, `flex-fields/` and `drive/`),
developed together and released in lockstep, plus two domain-agnostic infrastructure trees:
`aspnetcore-mcp/`, the shared MCP server that modules in
other repositories depend on, and `openiddict/`, additions to ABP's OpenIddict module for authorization servers.

## Repository-wide invariants

These are the things most likely to be broken by an otherwise reasonable-looking change:

1. **PackageIds and root namespaces never change to match the layout.** Every package keeps the ID it
   has always had; living in a subdirectory changed nothing for consumers (PackageId follows
   AssemblyName, not the folder). Never rename a package or root namespace to "match" the folders.
   Two renames are on record. `10.0.0-rc.24` moved the notifications packages to ABP's own
   naming (`.Abstractions` for contracts, the unsuffixed id for the default implementation, `.Client`
   for the remote publisher, `.Domain.Shared` / `.Domain` / `.EntityFrameworkCore` for the definition
   store) while the module was still pre-release, with a Migrate section in the CHANGELOG. Adopting an
   ABP convention before the first stable release is the only reason a rename has ever been accepted;
   it is not a precedent for renaming after a layout change.
   - **Rename on record, `10.0.0-rc.28`:** `file-storing/` became `blob-storing/`, and its packages
     `Dignite.Abp.FileStoring` / `Dignite.Abp.FileStoring.Imaging` became `Dignite.Abp.BlobStoring.Pipeline` /
     `Dignite.Abp.BlobStoring.Imaging` (`Dignite.Abp.BlobStoring.<Addition>`, mirroring
     `Volo.Abp.BlobStoring.<Provider>`), together with the removal of the `IFileHandler` pipeline that ABP 10.7's
     `IBlobPipelineContributor` replaced. This is the **same exception** as rc.24 — a pre-release module adopting an
     ABP naming convention, with a Migrate section in the CHANGELOG — not a new precedent: the folder moving
     was not the reason, and a rename after the first stable release is not allowed
     ([`blob-storing/dev-docs/pipeline-contributors-decision.md`](blob-storing/dev-docs/pipeline-contributors-decision.md)).
   - **Removal on record, `10.0.0-rc.25`:** `Dignite.FileExplorer.*` (Domain.Shared, Domain,
     Application.Contracts, Application, HttpApi, HttpApi.Client, EntityFrameworkCore, MongoDB, Mcp,
     Installer), `Dignite.Abp.FlexFields.FileExplorer` / `.FileExplorer.Web`, and the npm packages
     `@dignite/ng.file-explorer` / `@dignite/ng.flex-fields-file-explorer` are no longer published from
     this repository. The file browser became a Site feature in the `site` repository, under the Site
     namespace — new package identities there, not these IDs continued
     ([`blob-storing/dev-docs/core-only-decision.md`](blob-storing/dev-docs/core-only-decision.md)). Removing a
     package is not renaming one: the remaining IDs are untouched, and a removed ID is never reused here.
   - **Removal on record, `10.0.0-rc.28`:** the `Dignite.Abp.FileStoring*` IDs are retired by the rename above, not
     reused for anything else.

2. **Module trees reference each other only along declared edges, and the graph stays acyclic.**
   `blob-storing/`, `notifications/`, `flex-fields/` and `drive/` share this repository for development and
   release. A `ProjectReference` from one tree to another is allowed only when the referencing tree's `CLAUDE.md`
   names what it depends on; any other one is a bug, and nothing catches it: the aggregate `.slnx` contains all of
   them, so it compiles fine. `dotnet pack` turns an allowed reference into a same-version `PackageReference`
   (lockstep release, as in ABP's own monorepo). The one declared edge is `drive/` to `blob-storing/` (the Drive
   runs blob-storing's pipeline contributors); `blob-storing/`, `notifications/` and `flex-fields/` reference
   none of the others, and `blob-storing/`'s "what stays out" is unchanged
   ([`drive/dev-docs/drive-decision.md`](drive/dev-docs/drive-decision.md)).
   - `drive/` is the **open edition** of Dignite Drive: the storage tier and attaching a file (upload, read, delete).
     Its file library - listing, directories, rename, move, per-item grants, the Drive page - is **Drive Pro**
     (`Dignite.Drive.Pro.*`), a commercial product in a repository of its own that builds on `drive/`. The edge
     points one way, out of this repository: nothing here references Drive Pro, and open modules (Site, flex-fields)
     never depend on it: Site uses the open edition only. Drive Pro's code stays out of this repository and its
     history, and what is on its side of the split does not come back into `drive/` (`drive/CLAUDE.md`, invariant 8).
   - There used to be another shared top-level tree (`aspnetcore-mvc-razor/`) for generic ASP.NET
     Core MVC/Razor infrastructure with no domain model of its own. It was dissolved once it turned
     out to have exactly one consumer per file: each piece now lives directly in the one project
     that actually uses it (e.g. `Dignite.Abp.FlexFields.Web` owns its own
     `IRazorPartialRenderer`/`RazorPartialRenderer`/`AddCompiledRazorAssemblyPartIfNotExists`) rather
     than a shared tree with a single dependent. There is no longer a home in this repo for
     cross-module ASP.NET Core/Razor infrastructure — if a genuine second consumer shows up, judge
     fresh whether a shared tree is actually worth it rather than reflexively recreating one.
   - `aspnetcore-mcp/` (`Dignite.Abp.AspNetCore.Mcp`) is such a judged-fresh shared tree, and it is
     not a module: it hosts the one MCP server an application can have and carries no domain
     model. It passed the test the Razor tree failed because every piece of it is needed by every
     MCP-contributing module — `Dignite.Site.Mcp` and `Dignite.Vault.Extract.Mcp` in their own
     repositories (and `Dignite.Drive.Mcp` here; `Dignite.FileExplorer.Mcp` was another until File
     Explorer left in rc.25) — and
     duplicating it is exactly how two of them had already drifted apart (see its README). The direction is one-way: a module's `*.Mcp`
     project may reference `aspnetcore-mcp/`; `aspnetcore-mcp/` never references a module. Each
     contributing module claims its own MCP namespace (tool-name prefix + resource URI scheme) via
     `AddAbpMcpModule`, and the server refuses to start on any overlap.
   - `openiddict/` (`Dignite.Abp.OpenIddict.*`) is another infrastructure tree, not a module: additions to
     ABP's OpenIddict module for an **authorization server**, with no domain model of its own. Its first
     package, `Dignite.Abp.OpenIddict.ClientIdMetadataDocuments` (OAuth Client ID Metadata Documents), is
     needed by every authorization server MCP clients sign in to (shiji's HttpApi.Host, cloud's auth-server,
     vault-extract's host) and kept in one place because it is SSRF-sensitive code that follows the clients'
     behaviour. It is deliberately **not** in `aspnetcore-mcp/`: it fails that tree's test (its consumers are
     authorization servers, not MCP-contributing modules), and that tree's README and package metadata would be
     packed into it. `openiddict/` and `aspnetcore-mcp/` **never reference each other** in either direction: an
     authorization server need not host an MCP server, and an MCP server need not be an authorization server.
     Its packages depend only on ABP's OpenIddict packages, plug in through OpenIddict event handlers and replace
     none of OpenIddict's services, so each can be retired - with the whole tree, if it is the last - once
     OpenIddict covers it natively.

3. **Library package versions live in the root `Directory.Packages.props`**, never inline in a
   library `.csproj`. The demo hosts (`notifications/host/`, `flex-fields/demo/`, `drive/host/`) are the
   deliberate exception — each opts out of central package management and pins inline. `blob-storing/`
   has no demo host: it is a library of pipeline contributors with no API or UI to demonstrate.

## Documentation

- **`docs/` is the user documentation**, in the [ABP Docs module](https://abp.io/docs/latest/modules/docs) format
  that ABP's own `docs/` uses: one tree, `docs/docs-langs.json`, then `docs/en/docs-nav.json`, `docs/en/index.md`
  and one folder per module (`docs/en/notifications/`, …, each with an `index.md` and its own `images/`). Pages
  follow ABP's conventions — a `//[doc-seo]` block first, title-case headings, four-backtick code fences, relative
  links ending in `.md`. A site reads the tree from GitHub at each release tag, so every file under `docs/` is
  public and versioned; a page's file name is its address, so never rename a published page.
- **`docs/en/articles/<yyyy-MM-dd>-<slug>/post.md`** are dated articles, kept in the tree as ABP keeps its
  community articles, and left out of the navigation. They are snapshots: fix a broken link, never rewrite one
  to match a later release — the module's own pages carry the current behaviour.
- **`<module>/dev-docs/`** are design documents and decision records for contributors. They are not published to
  the site and are not linked from `docs/`.
- **`<module>/README.md`** is packed into every NuGet package of that module as its package README. The full
  documentation belongs in `docs/`; the README introduces the module and links there with absolute URLs, since
  nuget.org does not resolve links relative to the repository.
