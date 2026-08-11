# Dignite ABP Modules — monorepo guide

Three independently installable **ABP Framework** module trees, developed together and released in
lockstep.

## Repository-wide invariants

These are the things most likely to be broken by an otherwise reasonable-looking change:

1. **PackageIds and root namespaces never change.** Every package keeps the ID it has always had;
   living in a subdirectory changed nothing for consumers (PackageId follows AssemblyName, not the
   folder). Never rename a package or root namespace to "match" the layout.

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

3. **Library package versions live in the root `Directory.Packages.props`**, never inline in a
   library `.csproj`. The demo hosts (`file-storing/host/`, `notifications/host/`,
   `flex-fields/demo/`) are the deliberate exception — each opts out of central package management
   and pins inline.
