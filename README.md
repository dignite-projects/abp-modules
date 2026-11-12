# Dignite ABP Modules

Reusable **[ABP Framework](https://abp.io)** modules from [Dignite](https://github.com/dignite-projects),
developed together in one repository and released in lockstep.

> **.NET 10 · ABP 10.7.0 · LGPL-3.0-only**

| Module | What it is | Docs |
|---|---|---|
| [`blob-storing/`](blob-storing/) | Everyday **pipeline contributors** for ABP BlobStoring: the `IBlobPipelineContributor`s ABP 10.7 leaves out — a size cap, a content-type allow-list checked against the real bytes, GZip, image resize and compress — plus content-based MIME detection. No upload service, metadata, API or UI — the application that owns the files keeps those. | [Docs](docs/en/blob-storing/index.md) |
| [`blob-management/`](blob-management/) | **Blob Management**: the tenant's BLOB containers, managed. A BlobStoring provider applications keep their bytes with, and - for the people in front of them - directories, a file explorer and a file input, with permissions on a container and on a directory. An infrastructure module, hostable in the application's own process or once as its own service. In development, not yet released. | [Docs](docs/en/blob-management/index.md) |
| [`notifications/`](notifications/) | An extensible, event-driven **notification framework** with pluggable channel notifiers (SignalR, email, device push), plus an optional **Notification Center** (persistent inbox, subscriptions, read/unread state, REST API) with MVC and Angular UI libraries. | [Docs](docs/en/notifications/index.md) |
| [`flex-fields/`](flex-fields/) | Runtime-defined (**"flex"**) fields — a constraint kernel supplying field types, configuration, validation, a per-entity value bag and a derived query index, with EF Core and MongoDB providers, plus Angular and MVC UI. It owns no domain model: each consuming application defines its own fields. | [Docs](docs/en/flex-fields/index.md) |
| [`aspnetcore-mcp/`](aspnetcore-mcp/) | Shared infrastructure, not a module: hosts the one **MCP (Model Context Protocol) server** an ABP application can have, and lets each module contribute AI-callable tools, resources and prompts to it inside its own namespace (`Dignite.Site` and `Dignite.Vault.Extract` do, from their own repositories). | [Docs](docs/en/aspnetcore-mcp/index.md) |
| [`openiddict/`](openiddict/) | Infrastructure, not a module: additions to ABP's **OpenIddict** module for an authorization server — **Client ID Metadata Documents**, the client registration ChatGPT's and Claude's MCP connectors use, with no pre-registration and no dynamic registration. Independent of `aspnetcore-mcp/`. | [Docs](docs/en/openiddict/index.md) |

The documentation of every module starts at [`docs/en/index.md`](docs/en/index.md).

Each module is **independently installable**; the trees reference each other only along the edges their `CLAUDE.md`
files declare (today: `blob-management/` builds on `blob-storing/`), and a module's optional `*.Mcp` package depends only on the
shared `aspnetcore-mcp/` tree. They share this
repository for development and release, not at runtime. Every package keeps the PackageId it has
always had; moving into a subdirectory changed nothing for consumers. (The exceptions are the pre-release
renames that adopted ABP's package naming, recorded in [CLAUDE.md](CLAUDE.md) and the CHANGELOG.)

## History

These modules grew out of [`dignite-projects/dignite-abp`](https://github.com/dignite-projects/dignite-abp),
a broader, no-longer-maintained collection of ABP add-ons (notifications, dynamic forms, a file
manager, a theme, and more).

- [`blob-storing/`](blob-storing/) and [`notifications/`](notifications/) were each split out of
  `dignite-abp` into their own standalone repositories —
  [`abp-file-storing`](https://github.com/dignite-projects/abp-file-storing) and
  [`abp-notifications`](https://github.com/dignite-projects/abp-notifications) — and later merged
  into this monorepo with their full commit history preserved. No PackageId or root namespace
  changed in either move. The first of them has since become `blob-storing/`: it published
  `Dignite.Abp.FileStoring` until `10.0.0-rc.27`, and now publishes `Dignite.Abp.BlobStoring.Pipeline` and
  `Dignite.Abp.BlobStoring.Imaging`, pipeline contributors for ABP's own BLOB pipeline.
- [`flex-fields/`](flex-fields/) was extracted directly from `dignite-abp` (as
  `Dignite.Abp.DynamicForms`, renamed on the way out) straight into this repository, skipping the
  standalone-repo stage.

Because of that lineage, older commits and CHANGELOG entries reference issues and pull requests filed
against those earlier repositories, not this one — that history predates `abp-modules` itself and
isn't tracked in this repo's issue tracker.

## Repository layout

```
abp-modules/
├── Directory.Build.props        # shared metadata + the single <Version> all packages use
├── Directory.Packages.props     # central package management for every library project
├── global.json  NuGet.Config  .nvmrc
├── Dignite.Abp.Modules.slnx     # aggregate solution (every module)
├── .github/workflows/           # one build+test workflow, one lockstep release workflow, one PR labeler
├── build/                       # build/test scripts + the npm/Angular package verification scripts
├── docs/                        # user documentation for every module (ABP Docs format), and articles
├── aspnetcore-mcp/              # shared MCP server infrastructure (no domain model)
│   ├── Dignite.Abp.AspNetCore.Mcp.slnx   # focused solution for this tree alone
│   └── src/  test/                       # the published class library and its tests
├── blob-storing/
│   ├── Dignite.Abp.BlobStoring.slnx      # focused solution for this module alone
│   ├── src/  test/                       # the published class libraries and their tests (no demo host)
│   ├── dev-docs/                         # decision records (why contributors, why no File Explorer here)
│   └── .claude/skills/                   # module-specific conventions & invariants
├── blob-management/
│   ├── Dignite.Abp.BlobManagement.slnx   # focused solution for this module alone (src/, test/ and host/)
│   ├── src/  test/                       # the published class libraries and their tests
│   ├── host/  angular/                   # local-dev demo app + the Angular workspace holding @dignite/ng.blob-management
│   └── dev-docs/                         # design decisions (blob-management-decision.md is the current one)
├── notifications/
│   ├── Dignite.Abp.Notifications.slnx  # focused solution for this module alone
│   ├── core/  notification-center/       # the published class libraries
│   ├── host/  angular/                   # local-dev demo app + Angular workspace
│   ├── dev-docs/                         # design documents
│   └── .claude/skills/                   # module-specific conventions & invariants
├── flex-fields/
│   ├── Dignite.Abp.FlexFields.slnx       # focused solution for this module alone (src/, test/ and demo/)
│   ├── src/  test/                       # the published class libraries and their tests
│   ├── demo/  angular/                   # local-dev demo app + Angular workspace
│   └── dev-docs/flexfields-design.md     # design rationale
└── openiddict/                  # OpenIddict additions for authorization servers (no domain model)
    ├── Dignite.Abp.OpenIddict.slnx       # focused solution for this tree alone
    └── src/  test/                       # the published class library and its tests
```

`host/` (or `demo/`) and `angular/` under a module are **local-dev demos only** — they run and
exercise the stack end to end, and are never packed or published (`IsPackable=false`). A real
consuming application brings its own host.

## Build & test

The aggregate solution builds and tests every module:

```bash
dotnet build Dignite.Abp.Modules.slnx
dotnet test Dignite.Abp.Modules.slnx
```

To work on one module in isolation, use its own solution — same projects, smaller graph:

```bash
dotnet build blob-storing/Dignite.Abp.BlobStoring.slnx
dotnet build blob-management/Dignite.Abp.BlobManagement.slnx
dotnet build notifications/Dignite.Abp.Notifications.slnx
dotnet build flex-fields/Dignite.Abp.FlexFields.slnx
dotnet build aspnetcore-mcp/Dignite.Abp.AspNetCore.Mcp.slnx
dotnet build openiddict/Dignite.Abp.OpenIddict.slnx
```

`dotnet test` starts an embedded mongod (MongoSandbox) for the MongoDB provider tests and uses
in-memory SQLite for the EF Core ones, so no local database install is needed.

The Angular libraries are separate Node packages, outside MSBuild:

```bash
cd notifications/angular && npx yarn && npx yarn build:lib
cd flex-fields/angular   && npx yarn && npx yarn build:lib
cd blob-management/angular && npx yarn && npx yarn build:lib
```

## Versioning

**One version for the whole repository.** Every package in every module — NuGet and npm alike —
ships the same version, from the single `<Version>` in the root `Directory.Build.props`, on one
`v*` tag and one release pipeline. A change to one module bumps the version for all; the others get a
release whose content is unchanged. That trade is deliberate: one number to reason about instead of a
per-module compatibility matrix.

**`MAJOR.MINOR` is the ABP Framework version it is built on; `PATCH` is ours.** `10.7.x` targets ABP
10.7 (and so Angular 22); moving to ABP 10.8 releases `10.8.0`. A new ABP patch needs no release
here. Breaking changes only ship when `MAJOR.MINOR` moves, and new features can arrive in a `PATCH`
— see [CONTRIBUTING.md → Versioning and releases](CONTRIBUTING.md#versioning-and-releases) before
cutting a release or interpreting a version bump.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the development workflow, the versioning scheme, and the
release procedure. Security reports go through [SECURITY.md](SECURITY.md) — please don't open a
public issue for a vulnerability.

`blob-storing/` and `notifications/` document their architectural invariants under
`<module>/.claude/skills/` (written for AI-assisted contributions, but equally the reference for human
contributors). Start with `<module>/CLAUDE.md` for the layer map, then the module's `*-invariants`
skill — those encode the specific bugs each module was built or rewritten to fix, so they are the first
thing to read before changing load-bearing code. `flex-fields/` and `blob-management/` have a `CLAUDE.md` and their design rationale in `dev-docs/`; `aspnetcore-mcp/` and `openiddict/` are documented by their READMEs and `docs/`
([MCP Server](docs/en/aspnetcore-mcp/index.md), [OpenIddict](docs/en/openiddict/index.md)).

## License

[LGPL-3.0-only](LICENSE).
