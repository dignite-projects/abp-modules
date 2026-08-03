# Dignite ABP Modules — monorepo guide

Three independently installable **ABP Framework** module trees, developed together and released in
lockstep, plus one shared, domain-agnostic infrastructure tree (`aspnetcore-mvc-razor/`) that any of
them — or a downstream host — may depend on.

## Repository-wide invariants

These are the things most likely to be broken by an otherwise reasonable-looking change:

1. **PackageIds and root namespaces never change.** Every package keeps the ID it has always had;
   living in a subdirectory changed nothing for consumers (PackageId follows AssemblyName, not the
   folder). Never rename a package or root namespace to "match" the layout.

2. **The three modules never reference each other.** `file-storing/`, `notifications/`, and
   `flex-fields/` share this repository for development and release only. A `ProjectReference`
   across those boundaries is a bug — nothing catches it: the aggregate `.slnx` contains all three,
   so it compiles fine.
   - This is about the three *domain* modules specifically, not about the repository's top-level
     trees in general. `aspnetcore-mvc-razor/` is deliberately not a fourth one of "the three": it
     carries no domain model of its own (generic ASP.NET Core MVC/Razor plumbing — see its own
     README), and a module referencing it (`flex-fields/src/Dignite.Abp.FlexFields.Web` does) is not
     a cross-module reference in the sense this invariant guards against. If a second module ever
     needs the same infrastructure, it should depend on this tree too rather than duplicating it or
     reaching into a sibling module.

3. **Library package versions live in the root `Directory.Packages.props`**, never inline in a
   library `.csproj`. The demo hosts (`file-storing/host/`, `notifications/host/`,
   `flex-fields/demo/`) are the deliberate exception — each opts out of central package management
   and pins inline.
