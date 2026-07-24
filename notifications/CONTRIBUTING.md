# Contributing

## Development

See the root [README.md](./README.md) and [CLAUDE.md](./CLAUDE.md) for the solution layout, the
two operation modes, and the full build/test/pack commands. In short:

```bash
dotnet build Dignite.Abp.Notifications.slnx
dotnet test Dignite.Abp.Notifications.slnx
```

## Code conventions

This repo's architectural invariants and layer conventions are documented as Claude Code rules
under `.claude/rules/` (loaded automatically for AI-assisted contributions, but equally
applicable to human contributors) — start with `.claude/rules/template/app.md` for the layer map,
and `.claude/rules/framework/common/notifications-invariants.md` for the hard invariants around
`NotificationData`, Notifiers, and DI lifetimes.

## Versioning and releases

This project uses three-part [Semantic Versioning](https://semver.org/) (`MAJOR.MINOR.PATCH`), as
declared in [CHANGELOG.md](./CHANGELOG.md) — with one deliberate deviation from the classic
scheme, described below. SemVer is not cosmetic here: the version is still the stability signal to
downstream consumers, but MAJOR answers a different question than it does in a typical SemVer
project.

### MAJOR tracks the ABP Framework version, not this module's own breaking changes

This repo is a from-scratch rewrite of a legacy internal module whose own packages
(`Dignite.Abp.Notifications`, `Dignite.Abp.Notifications.Identity`) are already published on
NuGet.org under this same `dignite-projects` org, at versions `1.0.0` through `3.8.2`. To make
this rewrite's package unambiguously supersede that legacy version on NuGet.org's "latest version"
resolution — no package rename needed — this project's `<Version>` **MAJOR** segment tracks the
**major version of the ABP Framework** this release targets (pinned in `Directory.Packages.props`;
currently ABP `10.5.0`, so this module's MAJOR is `10`). **MINOR** and **PATCH** are this module's
own independent counters:

- **MINOR** — a backward-compatible addition to this module, **or** a breaking change to this
  module's own contracts. There is no separate signal below MAJOR for "this is breaking" under
  this scheme — read the CHANGELOG entry for a MINOR bump, don't assume safety from the version
  shape alone the way you would under classic SemVer.
- **PATCH** — a fix that changes no contract.
- MINOR and PATCH **reset to `.0.0`** whenever the tracked ABP major changes. If this repo later
  moves to target ABP 11.x, the version becomes `11.0.0`, not `11.5.3`.

This is not without precedent: several EF Core provider packages align `MAJOR.MINOR` with the EF
Core version they support, while PATCH remains their own. The legacy `dignite-abp` repo has
stopped publishing now that this repo has taken over the `Dignite.Abp.Notifications*` package
IDs, so there is no ongoing version-collision risk to design around beyond staying above `3.8.2` —
which tracking ABP's own major (currently `10`) satisfies trivially and permanently, since ABP's
major will not regress below 10.

### Pre-release suffixes

Use SemVer pre-release tags for previews on the way to a stable version:
`10.0.0-preview.1` → `10.0.0-rc.1` → `10.0.0`. Both NuGet and npm understand their precedence (a
suffixed version always ranks below the matching final version) and treat them as non-stable by
default. **The first published version of this project is `10.0.0-preview.1`**, not a stable
`10.0.0` — graduating to a stable version is an earned milestone (confidence in the contracts
across at least the persistence providers and the REST API), not a default for a first release.

**Do not use CalVer** (e.g. `2026.7.0`) — it communicates when a release was cut, not whether it's
safe to upgrade, which is the opposite of what this project's positioning needs.

### Where the version lives

| Property | Segments | Purpose |
|----------|----------|---------|
| `<Version>` in [`Directory.Build.props`](./Directory.Build.props) | 3-segment SemVer (+ optional pre-release suffix) | The NuGet package version for all 15 packable projects, and the value a `v*` tag must match. **This is the release version.** |
| `version` in [`angular/projects/notification-center/package.json`](./angular/projects/notification-center/package.json) | Same value as `<Version>` | The npm package version for `@dignite/ng.notification-center`; CI and the release workflow fail when it drifts from the NuGet version. |
| `<AssemblyVersion>` | 4-segment | Kept coarse and stable (`1.0.0.0`); not moved on every MINOR/PATCH, to avoid assembly-binding churn — see [`.claude/rules/framework/common/notifications-invariants.md`](.claude/rules/framework/common/notifications-invariants.md) §1. |
| Git tag | `vX.Y.Z[-suffix]` | Created on the release commit; the release workflow reads `<Version>` from `Directory.Build.props` and fails if the tag doesn't match — tags do not drive the version number. |
| `## [x.y.z]` heading in [`CHANGELOG.md`](./CHANGELOG.md) | 3-segment SemVer (+ optional pre-release suffix) | Human-facing release notes, extracted verbatim into the GitHub Release body. |

### Cutting a release

1. Move the CHANGELOG `[Unreleased]` section to `## [x.y.z] - YYYY-MM-DD`.
2. Confirm `<Version>` in `Directory.Build.props` and `version` in
   `angular/projects/notification-center/package.json` both match the intended release (tags do not
   drive the version — the release workflow reads and compares both files).
3. Tag and push: `git tag vX.Y.Z && git push origin vX.Y.Z`. The release workflow
   (`.github/workflows/release.yml`) triggers on `v*` tags; `workflow_dispatch` only builds and
   packs artifacts and does not create a GitHub Release.
4. **Immediately open the next development version**: bump `<Version>` in `Directory.Build.props`
   to the next pre-release (e.g. `10.0.0-preview.1` → `10.0.0-preview.2`) in a standalone
   `chore(release): bump version to X` commit. Because the release version is read from
   `Directory.Build.props` (not the tag), leaving it on the just-released value means the next
   `workflow_dispatch` build would re-emit artifacts that collide with the already-published
   package.
5. Tagged releases publish the NuGet packages to NuGet.org and the Angular library to npm. Pre-release
   npm versions use the `next` dist-tag; stable versions use `latest`. `workflow_dispatch` remains a
   private preview build and does not publish to either public registry. npm requires every package
   to have a `latest` tag, so when the first-ever public version is a pre-release it temporarily owns
   both `next` and `latest`; the first stable release moves `latest` to the stable version.
6. NuGet.org publishing uses Trusted Publishing rather than a stored API key. The NuGet.org policy
   must select the intended package owner and match GitHub repository `dignite-projects/abp-notifications`
   plus workflow file `release.yml`. Set the repository variable `NUGET_USER` to the NuGet profile
   name used by `NuGet/login@v1`; never use an email address for this value.
