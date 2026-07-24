# Dignite ABP Modules

Reusable **[ABP Framework](https://abp.io)** modules from [Dignite](https://github.com/dignite-projects),
developed together in one repository and released in lockstep.

> **.NET 10 · ABP 10.5.0 · LGPL-3.0-only**

| Module | What it is | Docs |
|---|---|---|
| [`file-storing/`](file-storing/) | An extensible **file-upload framework** layered on ABP BlobStoring (per-container `IFileHandler` pipeline: size limits, type checking, image resizing), plus an optional DDD **File Explorer** backend (directory tree, persisted file metadata, REST API) and an Angular UI library. | [README](file-storing/README.md) |
| [`notifications/`](notifications/) | An extensible, event-driven **notification framework** with pluggable channel notifiers (SignalR, email), plus an optional **Notification Center** (persistent inbox, subscriptions, read/unread state, REST API) with MVC and Angular UI libraries. | [README](notifications/README.md) |

Each module is **independently installable** — nothing in `file-storing/` references `notifications/`
or vice versa. They share this repository for development and release, not at runtime. Every package
keeps the PackageId it has always had; moving into a subdirectory changed nothing for consumers.

## Repository migration

The former standalone repositories are archived. Active development, issues, pull requests, and
releases now live in this monorepo:

| Former repository | New location |
|---|---|
| [`dignite-projects/abp-file-storing`](https://github.com/dignite-projects/abp-file-storing) | [`file-storing/`](file-storing/) |
| [`dignite-projects/abp-notifications`](https://github.com/dignite-projects/abp-notifications) | [`notifications/`](notifications/) |

Consumers do not need to rename packages or namespaces. Existing NuGet and npm package IDs are
unchanged; only the source repository and contribution target changed.

## Repository layout

```
abp-modules/
├── Directory.Build.props        # shared metadata + the single <Version> all packages use
├── Directory.Packages.props     # central package management for every library project
├── global.json  NuGet.Config  .nvmrc
├── Dignite.Abp.Modules.slnx     # aggregate solution (both modules)
├── .github/workflows/           # one build+test workflow, one lockstep release workflow
├── file-storing/
│   ├── Dignite.Abp.FileStoring.slnx      # focused solution for this module alone
│   ├── core/  file-explorer/             # the published class libraries
│   ├── host/  angular/                   # local-dev demo app + Angular workspace
│   └── .claude/rules/                    # module-specific conventions & invariants
└── notifications/
    ├── Dignite.Abp.Notifications.slnx    # focused solution for this module alone
    ├── core/  notification-center/       # the published class libraries
    ├── host/  angular/                   # local-dev demo app + Angular workspace
    └── .claude/rules/                    # module-specific conventions & invariants
```

`host/` and `angular/` under each module are **local-dev demos only** — they run and exercise the
stack end to end, and are never packed or published (`IsPackable=false`). A real consuming
application brings its own host.

## Build & test

The aggregate solution builds and tests both modules:

```bash
dotnet build Dignite.Abp.Modules.slnx
dotnet test Dignite.Abp.Modules.slnx
```

To work on one module in isolation, use its own solution — same projects, smaller graph:

```bash
dotnet build file-storing/Dignite.Abp.FileStoring.slnx
dotnet build notifications/Dignite.Abp.Notifications.slnx
```

`dotnet test` starts an embedded mongod (MongoSandbox) for the MongoDB provider tests and uses
in-memory SQLite for the EF Core ones, so no local database install is needed.

The Angular libraries are npm workspaces, outside MSBuild:

```bash
cd file-storing/angular  && npm install --legacy-peer-deps && npm run build:lib
cd notifications/angular && npm install && npm run build:lib
```

## Versioning

**One version for the whole repository.** Every package in every module — NuGet and npm alike —
ships the same version, from the single `<Version>` in the root `Directory.Build.props`, on one
`v*` tag and one release pipeline. A change to one module bumps the version for both; the other
module gets a release whose content is unchanged. That trade is deliberate: one number to reason
about instead of a per-module compatibility matrix.

`MAJOR` tracks the **ABP Framework major version** this release targets, not this repository's own
breaking changes. See [CONTRIBUTING.md → Versioning and releases](CONTRIBUTING.md#versioning-and-releases)
before cutting a release or interpreting a version bump — the scheme deviates from classic SemVer in
one specific, easy-to-misread way.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the development workflow, the versioning scheme, and the
release procedure. Security reports go through [SECURITY.md](SECURITY.md) — please don't open a
public issue for a vulnerability.

Each module documents its own architectural invariants under `<module>/.claude/rules/` (written for
AI-assisted contributions, but equally the reference for human contributors). Start with
`<module>/.claude/rules/template/app.md` for the layer map, then the module's `*-invariants.md` —
those encode the specific bugs each module was built or rewritten to fix, so they are the first
thing to read before changing load-bearing code.

## License

[LGPL-3.0-only](LICENSE).
