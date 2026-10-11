# Dignite ABP Modules

Reusable **[ABP Framework](https://abp.io)** modules from [Dignite](https://github.com/dignite-projects),
developed together in one repository and released in lockstep.

> **.NET 10 · ABP 10.7.0 · LGPL-3.0-only**

| Module | What it is | Docs |
|---|---|---|
| [`blob-storing/`](blob-storing/) | Everyday **pipeline contributors** for ABP BlobStoring: the `IBlobPipelineContributor`s ABP 10.7 leaves out — a size cap, a content-type allow-list checked against the real bytes, GZip, image resize and compress — plus content-based MIME detection. No upload service, metadata, API or UI — the application that owns the files keeps those. | [README](blob-storing/README.md) |
| [`notifications/`](notifications/) | An extensible, event-driven **notification framework** with pluggable channel notifiers (SignalR, email), plus an optional **Notification Center** (persistent inbox, subscriptions, read/unread state, REST API) with MVC and Angular UI libraries. | [README](notifications/README.md) |
| [`flex-fields/`](flex-fields/) | Runtime-defined (**"flex"**) fields — a constraint kernel supplying field types, configuration, validation, a per-entity value bag and a derived query index, with EF Core and MongoDB providers, plus an Angular UI library. It owns no domain model: each consuming application defines its own fields. | [README](flex-fields/README.md) |
| [`aspnetcore-mcp/`](aspnetcore-mcp/) | Shared infrastructure, not a module: hosts the one **MCP (Model Context Protocol) server** an ABP application can have, and lets each module contribute AI-callable tools to it inside its own namespace (`Dignite.Site` and `Dignite.Vault.Extract` do, from their own repositories). | [README](aspnetcore-mcp/README.md) |

Each module is **independently installable** — no module references another; a module's optional
`*.Mcp` package depends only on the shared `aspnetcore-mcp/` tree. They share this
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
├── .github/workflows/           # one build+test workflow, one lockstep release workflow
├── aspnetcore-mcp/              # shared MCP server infrastructure (no domain model)
├── blob-storing/
│   ├── Dignite.Abp.BlobStoring.slnx      # focused solution for this module alone
│   ├── src/  test/                       # the published class libraries and their tests (no demo host)
│   ├── docs/                             # decision records (why contributors, why no File Explorer here)
│   └── .claude/skills/                   # module-specific conventions & invariants
├── notifications/
│   ├── Dignite.NotificationCenter.slnx   # focused solution for this module alone
│   ├── core/  notification-center/       # the published class libraries
│   ├── host/  angular/                   # local-dev demo app + Angular workspace
│   └── .claude/skills/                   # module-specific conventions & invariants
└── flex-fields/
    ├── Dignite.Abp.FlexFields.slnx       # the demo host (the libraries build via the aggregate)
    ├── src/                              # the published class libraries
    ├── demo/  angular/                   # local-dev demo app + Angular workspace
    └── docs/flexfields-design.md         # design rationale
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
dotnet build notifications/Dignite.NotificationCenter.slnx
```

`flex-fields` has no such focused solution for its libraries — `Dignite.Abp.FlexFields.slnx` holds
only the demo host, so build its libraries through the aggregate solution.

`dotnet test` starts an embedded mongod (MongoSandbox) for the MongoDB provider tests and uses
in-memory SQLite for the EF Core ones, so no local database install is needed.

The Angular libraries are separate Node packages, outside MSBuild:

```bash
cd notifications/angular && npx yarn && npx yarn build:lib
cd flex-fields/angular   && npx yarn && npx yarn build:lib
```

## Versioning

**One version for the whole repository.** Every package in every module — NuGet and npm alike —
ships the same version, from the single `<Version>` in the root `Directory.Build.props`, on one
`v*` tag and one release pipeline. A change to one module bumps the version for all; the others get a
release whose content is unchanged. That trade is deliberate: one number to reason about instead of a
per-module compatibility matrix.

`MAJOR` tracks the **ABP Framework major version** this release targets, not this repository's own
breaking changes. See [CONTRIBUTING.md → Versioning and releases](CONTRIBUTING.md#versioning-and-releases)
before cutting a release or interpreting a version bump — the scheme deviates from classic SemVer in
one specific, easy-to-misread way.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the development workflow, the versioning scheme, and the
release procedure. Security reports go through [SECURITY.md](SECURITY.md) — please don't open a
public issue for a vulnerability.

Each module documents its own architectural invariants under `<module>/.claude/skills/` (written for
AI-assisted contributions, but equally the reference for human contributors). Start with
`<module>/CLAUDE.md` for the layer map, then the module's `*-invariants` skill — those encode the
specific bugs each module was built or rewritten to fix, so they are the first thing to read before
changing load-bearing code.

## License

[LGPL-3.0-only](LICENSE).
