# Dignite ABP Modules — monorepo guide

Three independently installable **ABP Framework** module trees, developed together and released in
lockstep, plus one shared, domain-agnostic infrastructure tree (`aspnetcore-mcp/`) that modules here
and in other repositories depend on.

## Repository-wide invariants

These are the things most likely to be broken by an otherwise reasonable-looking change:

1. **PackageIds and root namespaces never change to match the layout.** Every package keeps the ID it
   has always had; living in a subdirectory changed nothing for consumers (PackageId follows
   AssemblyName, not the folder). Never rename a package or root namespace to "match" the folders.
   The one rename on record is `10.0.0-rc.24`, which moved the notifications packages to ABP's own
   naming (`.Abstractions` for contracts, the unsuffixed id for the default implementation, `.Client`
   for the remote publisher, `.Domain.Shared` / `.Domain` / `.EntityFrameworkCore` for the definition
   store) while the module was still pre-release, with a Migrate section in the CHANGELOG. Adopting an
   ABP convention before the first stable release is the only reason a rename has ever been accepted;
   it is not a precedent for renaming after a layout change.

2. **The three modules never reference each other.** `file-storing/`, `notifications/`, and
   `flex-fields/` share this repository for development and release only. A `ProjectReference`
   across those boundaries is a bug — nothing catches it: the aggregate `.slnx` contains all three,
   so it compiles fine.
   - There used to be a fourth, shared top-level tree (`aspnetcore-mvc-razor/`) for generic ASP.NET
     Core MVC/Razor infrastructure with no domain model of its own. It was dissolved once it turned
     out to have exactly one consumer per file: each piece now lives directly in the one project
     that actually uses it (e.g. `Dignite.Abp.FlexFields.Web` owns its own
     `IRazorPartialRenderer`/`RazorPartialRenderer`/`AddCompiledRazorAssemblyPartIfNotExists`) rather
     than a shared tree with a single dependent. There is no longer a home in this repo for
     cross-module ASP.NET Core/Razor infrastructure — if a genuine second consumer shows up, judge
     fresh whether a shared tree is actually worth it rather than reflexively recreating one.
   - `aspnetcore-mcp/` (`Dignite.Abp.AspNetCore.Mcp`) is such a judged-fresh shared tree, and it is
     not a fourth module: it hosts the one MCP server an application can have and carries no domain
     model. It passed the test the Razor tree failed because every piece of it is needed by every
     MCP-contributing module — `Dignite.FileExplorer.Mcp` here, `Dignite.Site.Mcp` and
     `Dignite.Vault.Extract.Mcp` in their own repositories — and duplicating it is exactly how two of
     them had already drifted apart (see its README). The direction is one-way: a module's `*.Mcp`
     project may reference `aspnetcore-mcp/`; `aspnetcore-mcp/` never references a module. Each
     contributing module claims its own MCP namespace (tool-name prefix + resource URI scheme) via
     `AddAbpMcpModule`, and the server refuses to start on any overlap.

3. **Library package versions live in the root `Directory.Packages.props`**, never inline in a
   library `.csproj`. The demo hosts (`file-storing/host/`, `notifications/host/`,
   `flex-fields/demo/`) are the deliberate exception — each opts out of central package management
   and pins inline.
