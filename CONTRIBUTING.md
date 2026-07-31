# Contributing

## Development

See the root [README.md](./README.md) for the repository layout and the full build/test commands.
In short:

```bash
# All modules
dotnet build Dignite.Abp.Modules.slnx
dotnet test Dignite.Abp.Modules.slnx

# One module in isolation (same projects, smaller graph)
dotnet build file-storing/Dignite.FileExplorer.slnx
dotnet build notifications/Dignite.NotificationCenter.slnx
```

The Angular libraries are npm workspaces, built separately from MSBuild:

```bash
cd file-storing/angular  && npm install --legacy-peer-deps && npm run build:lib
cd notifications/angular && npm install && npm run build:lib
cd flex-fields/angular   && npm install --legacy-peer-deps && npm run build:lib
```

## Code conventions

Architectural invariants and layer conventions are documented in `CLAUDE.md` files and Claude Code
**skills** — loaded automatically for AI-assisted contributions, but equally the reference for human
contributors. They're split by how the content loads:

- **`<module>/CLAUDE.md`** — the module's orientation doc: structure, layer map, the "add a feature"
  flow, and a list of tripwires that mean "go read the invariants."
- **Root `.claude/skills/`** — 16 generic `abp-*` skills covering ABP itself (`abp-core`, `abp-ddd`,
  `abp-application-layer`, `abp-authorization`, `abp-ef-core`, `abp-mongodb`, `abp-testing`, …).
  These carry no module-specific content.
- **`<module>/.claude/skills/`** — two per module: a **`*-conventions`** skill ("how *this* module
  applies ABP") and a **`*-invariants`** skill ("what a change must not break").

`file-storing/`, `notifications/`, and `flex-fields/` have genuinely different DDD shapes,
persistence conventions, and hard invariants. Where they disagree — repository convention, object
mapper, distributed-event posture, test naming — **each module states its own** rather than sharing
an averaged rule that would be wrong for all three. Where a module's `*-conventions` skill disagrees
with a generic `abp-*` skill, the module skill wins for code in that module.

Start with:

- `<module>/CLAUDE.md` — the layer map and the "add a feature" flow.
- `file-storing/.claude/skills/file-storing-invariants/` — before touching the upload pipeline,
  blob/DB writes, directory moves, authorization, or a DI lifetime.
- `notifications/.claude/skills/notifications-invariants/` — before touching `NotificationData`,
  any Notifier, the distributor, or a DI lifetime.

Those invariant skills encode the exact bugs each module was built or rewritten to fix. They are not
style preferences — don't reintroduce what they rule out.

### Central package management

All library package versions live in the root [`Directory.Packages.props`](./Directory.Packages.props)
(`ManagePackageVersionsCentrally=true`). A library `.csproj` carries
`<PackageReference Include="..." />` with **no** `Version=`. To add or bump a dependency, edit
`Directory.Packages.props`, grouped under the matching `<ItemGroup Label="...">`.

The demo hosts are the deliberate exception: each opts out of central package management and
pins its own versions inline, via its own `Directory.Build.props` + `Directory.Packages.props`
inside the project folder (`file-storing/host/.../`, `notifications/host/.../`,
`flex-fields/demo/.../`). They are never published, so their dependency graph is allowed to drift
from the libraries'.

## Versioning and releases

This project uses three-part [Semantic Versioning](https://semver.org/) (`MAJOR.MINOR.PATCH`), as
declared in [CHANGELOG.md](./CHANGELOG.md) — with two deliberate deviations from the classic scheme,
described below. SemVer is not cosmetic here: the version is still the stability signal to
downstream consumers, but MAJOR answers a different question than it does in a typical SemVer
project, and the version is repository-wide rather than per-module.

### One version for the whole repository (lockstep)

**Every package in this repository ships the same version**, from the single `<Version>` in the root
[`Directory.Build.props`](./Directory.Build.props): all 33 packable NuGet projects across the three
module trees and all three Angular npm packages. One `v*` tag, one release pipeline, one number.

The consequence is **empty bumps**: a change to `file-storing/` alone still releases a new version of
every other module's package, whose content is identical to the previous release. That is accepted on
purpose. The alternative — independent per-module versions in one repository — means consumers
must reason about which `Dignite.Abp.Notifications` version pairs with which
`Dignite.Abp.FileStoring` version, and the maintainers must run two release pipelines and two tag
namespaces. One number that sometimes moves for no reason is cheaper than a compatibility matrix
that always needs checking.

There is therefore **no per-module versioning** and no per-project `<Version>`. Don't add one.

### MAJOR tracks the ABP Framework version, not this repository's own breaking changes

`file-storing/` and `notifications/` supersede legacy package lines already published on NuGet.org under this same
`dignite-projects` org at versions `1.0.0` through `3.8.2` (`Dignite.Abp.Notifications*` and
`Dignite.FileExplorer.*`). To make these releases unambiguously win NuGet.org's "latest version"
resolution — no package rename needed — `<Version>`'s **MAJOR** segment tracks the **major version
of the ABP Framework** this release targets (pinned in `Directory.Packages.props`; currently ABP
`10.5.0`, so MAJOR is `10`). Since ABP's major will not regress below 10, this permanently clears
the legacy `3.8.2` line.

**MINOR** and **PATCH** are this repository's own counters:

- **MINOR** — a backward-compatible addition, **or** a breaking change to either module's contracts.
  There is no separate signal below MAJOR for "this is breaking" under this scheme — read the
  CHANGELOG entry for a MINOR bump, don't assume safety from the version shape alone the way you
  would under classic SemVer.
- **PATCH** — a fix that changes no contract.
- MINOR and PATCH **reset to `.0.0`** whenever the tracked ABP major changes. Moving to target ABP
  11.x makes the version `11.0.0`, not `11.5.3`.

This is not without precedent: several EF Core provider packages align `MAJOR.MINOR` with the EF
Core version they support, while PATCH remains their own.

### Pre-release suffixes

Use SemVer pre-release tags for previews on the way to a stable version:
`10.0.0-preview.1` → `10.0.0-rc.1` → `10.0.0`. Both NuGet and npm understand their precedence (a
suffixed version always ranks below the matching final version) and treat them as non-stable by
default. Graduating to a stable version is an earned milestone — confidence in the contracts across
both persistence providers and the REST APIs — not a default for a first release.

**Do not use CalVer** (e.g. `2026.7.0`) — it communicates when a release was cut, not whether it's
safe to upgrade, which is the opposite of what this project's positioning needs.

### Where the version lives

| Property | Segments | Purpose |
|----------|----------|---------|
| `<Version>` in [`Directory.Build.props`](./Directory.Build.props) | 3-segment SemVer (+ optional pre-release suffix) | The NuGet package version for **all 33 packable projects across all three modules**, and the value a `v*` tag must match. **This is the release version.** |
| `version` in [`file-storing/angular/projects/file-explorer/package.json`](./file-storing/angular/projects/file-explorer/package.json) | Same value as `<Version>` | npm version for `@dignite/ng.file-explorer`. |
| `version` in [`notifications/angular/projects/notification-center/package.json`](./notifications/angular/projects/notification-center/package.json) | Same value as `<Version>` | npm version for `@dignite/ng.notification-center`. |
| `version` in [`flex-fields/angular/projects/flex-fields/package.json`](./flex-fields/angular/projects/flex-fields/package.json) | Same value as `<Version>` | npm version for `@dignite/ng.flex-fields`. |
| `<AssemblyVersion>` | 4-segment | Pinned at `1.0.0.0` and **never** bumped with `<Version>`, avoiding assembly-binding churn. Load-bearing for notifications specifically — see [`notifications-invariants`](./notifications/.claude/skills/notifications-invariants/SKILL.md) §1. Don't "fix" this to match `<Version>`. |
| Git tag | `vX.Y.Z[-suffix]` | Created on the release commit; the release workflow reads `<Version>` and fails if the tag doesn't match — tags do not drive the version number. |
| `## [x.y.z]` heading in [`CHANGELOG.md`](./CHANGELOG.md) | 3-segment SemVer (+ optional pre-release suffix) | Human-facing release notes, extracted verbatim into the GitHub Release body. |

CI and the release workflow both run
[`.github/scripts/verify-version-lockstep.ps1`](./.github/scripts/verify-version-lockstep.ps1),
which fails the build if `<Version>` and **any** Angular package version drift apart.

### Cutting a release

1. Move the CHANGELOG `[Unreleased]` section to `## [x.y.z] - YYYY-MM-DD`, keeping entries grouped
   by module so readers can tell which half of the repo changed.
2. Confirm `<Version>` in `Directory.Build.props` and `version` in **all three** Angular
   `package.json` files match the intended release (tags do not drive the version — the release
   workflow reads and compares all three).
3. Tag and push: `git tag vX.Y.Z && git push origin vX.Y.Z`. The release workflow
   (`.github/workflows/release.yml`) triggers on `v*` tags; `workflow_dispatch` only builds and
   packs artifacts and does not create a GitHub Release.
4. **Immediately open the next development version**: bump `<Version>` and all three Angular package
   versions to the next pre-release in a standalone `chore(release): bump version to X` commit.
   Because the release version is read from `Directory.Build.props` (not the tag), leaving it on the
   just-released value means the next `workflow_dispatch` build would re-emit artifacts that collide
   with the already-published packages.
5. Tagged releases publish the NuGet packages to NuGet.org and **all three** Angular libraries to npm.
   Pre-release npm versions use the `next` dist-tag; stable versions use `latest`.
   `workflow_dispatch` remains a private preview build and does not publish to either public
   registry. npm requires every package to have a `latest` tag, so when a package's first-ever
   public version is a pre-release it temporarily owns both `next` and `latest`; the first stable
   release moves `latest` to the stable version.
6. NuGet.org publishing uses Trusted Publishing rather than a stored API key. The NuGet.org policy
   must select the intended package owner and match GitHub repository
   `dignite-projects/abp-modules` plus workflow file `release.yml`. Set the repository variable
   `NUGET_USER` to the NuGet profile name used by `NuGet/login@v1`; never use an email address for
   this value.

> **Migrating from the old repositories:** these packages were previously released from
> `dignite-projects/abp-file-storing` and `dignite-projects/abp-notifications`. Each package's
> NuGet.org Trusted Publishing policy must be repointed to this repository and its `release.yml`,
> or the release will fail at the publishing step.
