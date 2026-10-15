# Contributing

## Development

See the root [README.md](./README.md) for the repository layout and the full build/test commands.
In short:

```bash
# All modules
dotnet build Dignite.Abp.Modules.slnx
dotnet test Dignite.Abp.Modules.slnx

# One module in isolation (same projects, smaller graph)
dotnet build blob-storing/Dignite.Abp.BlobStoring.slnx
dotnet build notifications/Dignite.Abp.Notifications.slnx
```

The Angular libraries live in two Angular CLI workspaces, installed with Yarn Classic (v1) and built
separately from MSBuild. (`blob-storing/` has none: it is a .NET library layer only.)

```bash
cd notifications/angular && yarn install && yarn build:lib
cd flex-fields/angular   && yarn install && yarn build:lib && yarn build:lib:ckeditor
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
- **`<module>/.claude/skills/`** — in `blob-storing/` and `notifications/`, two per module: a
  **`*-conventions`** skill ("how *this* module applies ABP") and a **`*-invariants`** skill ("what a
  change must not break"). `flex-fields/` has none; its `CLAUDE.md` and design doc carry that content.

`blob-storing/`, `notifications/`, and `flex-fields/` have genuinely different DDD shapes,
persistence conventions, and hard invariants. Where they disagree — repository convention, object
mapper, distributed-event posture, test naming — **each module states its own** rather than sharing
an averaged rule that would be wrong for all three. Where a module's `*-conventions` skill disagrees
with a generic `abp-*` skill, the module skill wins for code in that module.

Start with:

- `<module>/CLAUDE.md` — the layer map and the "add a feature" flow.
- `blob-storing/.claude/skills/blob-storing-invariants/` — before touching a contributor's stream
  handling, buffering, MIME detection, an image contributor, or a stored format.
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
inside the project folder (`notifications/host/.../`,
`flex-fields/demo/.../`). They are never published, so their dependency graph is allowed to drift
from the libraries'.

## Versioning and releases

This project uses three-part [Semantic Versioning](https://semver.org/) (`MAJOR.MINOR.PATCH`), as
declared in [CHANGELOG.md](./CHANGELOG.md) — with two deliberate deviations from the classic scheme,
described below. SemVer is not cosmetic here: the version is still the stability signal to
downstream consumers, but MAJOR.MINOR answers a different question than it does in a typical SemVer
project, and the version is repository-wide rather than per-module.

### One version for the whole repository (lockstep)

**Every package in this repository ships the same version**, from the single `<Version>` in the root
[`Directory.Build.props`](./Directory.Build.props): every packable NuGet project across the three
module trees and all three Angular npm packages. One `v*` tag, one release pipeline, one number.

The consequence is **empty bumps**: a change to `blob-storing/` alone still releases a new version of
every other module's package, whose content is identical to the previous release. That is accepted on
purpose. The alternative — independent per-module versions in one repository — means consumers
must reason about which `Dignite.Abp.Notifications` version pairs with which
`Dignite.Abp.BlobStoring.Pipeline` version, and the maintainers must run two release pipelines and two tag
namespaces. One number that sometimes moves for no reason is cheaper than a compatibility matrix
that always needs checking.

There is therefore **no per-module versioning** and no per-project `<Version>`. Don't add one.

### MAJOR.MINOR is the ABP Framework version; PATCH is ours

`<Version>`'s **MAJOR.MINOR** is the **MAJOR.MINOR of the ABP Framework** this release is built
against (the `Volo.Abp.*` pins in `Directory.Packages.props`; currently ABP `10.7.0`, so releases are
`10.7.x`). A developer reading `10.7.2` knows, without opening a README, that it targets ABP 10.7 —
and, since ABP's minor decides the Angular major, which Angular it pairs with.
[`verify-version-lockstep.ps1`](./.github/scripts/verify-version-lockstep.ps1) enforces it: CI fails
if `<Version>` does not start with the `Volo.Abp.Core` MAJOR.MINOR, or if any `@abp/ng.*` dependency
of an Angular package is not a `~MAJOR.MINOR.x` range.

**PATCH** is this repository's own counter, not ABP's patch:

| What happened | Version |
|---|---|
| A fix or a backward-compatible addition here, still on the same ABP minor | PATCH + 1 (`10.7.0` → `10.7.1`) |
| ABP ships a patch (`10.7.3`) and nothing here needs to change | **No release.** ABP patches are compatible within their minor, and the `Volo.Abp.*` dependency is a floor (`>= 10.7.0`), so consumers already pick it up |
| This repository moves to a new ABP minor or major | `MAJOR.MINOR` of that ABP, PATCH reset to `0` (`10.8.0`, `11.0.0`) — released even if nothing else changed, because the version is the claim that it was verified on that ABP line |

Two consequences of giving MINOR away to ABP:

- **New features land in PATCH.** Classic SemVer would bump MINOR for them; here MINOR is taken.
  Read the CHANGELOG to tell a fix release from a feature release.
- **Breaking changes wait for the next ABP minor.** A PATCH release never breaks a contract; a
  breaking change ships with the next `MAJOR.MINOR` move, under a **Breaking** entry with a Migrate
  section in the CHANGELOG. ABP itself makes small breaking changes at its own minors, so consumers
  already expect to read notes at that boundary — ours line up with it instead of adding a second one.

Why not the alternatives: mirroring ABP's full version (`10.7.0` = ABP 10.7.0) leaves no number for
this repository's own fixes between ABP patches; a fourth segment (`10.7.0.3`) is not valid on npm,
and our npm and NuGet packages share one version; build metadata (`1.2.0+abp.10.7`) is ignored by
both registries' ordering and hidden in most UIs. EF Core provider packages use the same
`MAJOR.MINOR`-aligned scheme.

Why MAJOR can never drop below 10: `notifications/` supersedes a legacy package line already
published on NuGet.org under this same `dignite-projects` org at versions `1.0.0` through `3.8.2`
(`Dignite.Abp.Notifications*`; the other, `Dignite.FileExplorer.*`, left this repository in rc.25).
Tracking ABP's major makes these releases win NuGet.org's "latest version" resolution with no package
rename, and ABP's major will not regress below 10.

### Pre-release suffixes

Use SemVer pre-release tags for previews on the way to a stable version, typically while moving to
the next ABP minor: `10.8.0-rc.1` → `10.8.0`. Both NuGet and npm understand their precedence (a
suffixed version always ranks below the matching final version) and treat them as non-stable by
default. The earlier pre-release line `10.0.0-preview.1` … `10.0.0-rc.29` predates this scheme; the first stable
release is `10.7.0`, and `10.0.0` itself was never published.

### Pre-releases: GitHub Packages only

**NuGet.org and npmjs only ever receive stable versions.** Every pre-release (`-rc.N`, `-preview.N`),
whether cut from a tag or a `workflow_dispatch`, is published to this org's GitHub Packages feeds only,
for this org's own projects to test against before the stable release. Pre-releases up to
`10.0.0-rc.29` did reach the public registries; they stay there, since NuGet.org versions cannot be
deleted, and are simply not added to.

| | NuGet | npm |
|---|---|---|
| Feed | `https://nuget.pkg.github.com/dignite-projects/index.json` | `https://npm.pkg.github.com` |
| Package names | Same IDs as on NuGet.org (`Dignite.Abp.Notifications`, …) | `@dignite-projects/ng.*` — GitHub Packages requires the scope to equal the org, so `@dignite/ng.x` is renamed |

Both feeds require authentication even to read: a personal access token (classic) with
`read:packages`, or in a consuming repository's own Actions, its `GITHUB_TOKEN` once that repository
has been granted access to the packages.

**NuGet** — add the feed next to NuGet.org in the consuming solution's `nuget.config`, then reference
the pre-release version as usual:

```xml
<packageSources>
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  <add key="dignite-projects" value="https://nuget.pkg.github.com/dignite-projects/index.json" />
</packageSources>
<packageSourceCredentials>
  <dignite-projects>
    <add key="Username" value="<github user>" />
    <add key="ClearTextPassword" value="%GITHUB_PACKAGES_TOKEN%" />
  </dignite-projects>
</packageSourceCredentials>
```

**npm** — point the `@dignite-projects` scope at GitHub Packages in the consuming workspace's
`.npmrc`, and install each package under its real `@dignite/*` name through an npm alias, so no
`import` changes:

```ini
@dignite-projects:registry=https://npm.pkg.github.com
//npm.pkg.github.com/:_authToken=${GITHUB_PACKAGES_TOKEN}
```

```json
"@dignite/ng.flex-fields": "npm:@dignite-projects/ng.flex-fields@10.8.0-rc.1",
"@dignite/ng.flex-fields-ckeditor": "npm:@dignite-projects/ng.flex-fields-ckeditor@10.8.0-rc.1"
```

The release workflow publishes each GitHub Packages copy with its own `@dignite/*` dependencies
rewritten to that same alias form
([`build/rewrite-for-github-packages.mjs`](./build/rewrite-for-github-packages.mjs)) — the
`flex-fields-ckeditor` copy depends on
`"@dignite/ng.flex-fields": "npm:@dignite-projects/ng.flex-fields@10.8.0-rc.1"`. Write the consumer's
alias in exactly that form: an identical spec is one Yarn Classic lockfile entry and one installed
copy, which is what issue #211 needs. Switching to the stable release is replacing each alias with the
plain version (`"@dignite/ng.flex-fields": "10.8.0"`).

**A downstream project's public release never depends on a pre-release.** Its own stable packages
must reference a stable version of these packages, or its consumers cannot restore it from NuGet.org.
That includes the cross-repository `ProjectReference` case under [Cutting a release](#cutting-a-release):
`site` packed while this workspace sits on `10.8.0-rc.1` pins `>= 10.8.0-rc.1`, which exists only on
GitHub Packages. Cut a downstream public release only while this workspace is on a stable version.

**Do not use CalVer** (e.g. `2026.7.0`) — it communicates when a release was cut, not whether it's
safe to upgrade, which is the opposite of what this project's positioning needs.

### Where the version lives

| Property | Segments | Purpose |
|----------|----------|---------|
| `<Version>` in [`Directory.Build.props`](./Directory.Build.props) | 3-segment SemVer (+ optional pre-release suffix) | The NuGet package version for **every packable project across all three modules**, and the value a `v*` tag must match. **This is the release version.** |
| `version` in [`notifications/angular/projects/notification-center/package.json`](./notifications/angular/projects/notification-center/package.json) | Same value as `<Version>` | npm version for `@dignite/ng.notification-center`. |
| `version` in [`flex-fields/angular/projects/flex-fields/package.json`](./flex-fields/angular/projects/flex-fields/package.json) | Same value as `<Version>` | npm version for `@dignite/ng.flex-fields`. |
| `version` in [`flex-fields/angular/projects/flex-fields-ckeditor/package.json`](./flex-fields/angular/projects/flex-fields-ckeditor/package.json) | Same value as `<Version>` | npm version for `@dignite/ng.flex-fields-ckeditor`. |
| `@dignite/*` entries in those `package.json` files' `dependencies` / `peerDependencies` | Same value as `<Version>`, exact — never a `^` range | One package from this repository depending on another (the `flex-fields-ckeditor` bolt-on on `@dignite/ng.flex-fields`). A range would only ever admit an *older* sibling, which is how a host ends up with two copies of `@dignite/ng.flex-fields` — see issue #211. |
| `<AssemblyVersion>` | 4-segment | Pinned at `1.0.0.0` and **never** bumped with `<Version>`, avoiding assembly-binding churn. Load-bearing for notifications specifically — see [`notifications-invariants`](./notifications/.claude/skills/notifications-invariants/SKILL.md) §1. Don't "fix" this to match `<Version>`. |
| Git tag | `vX.Y.Z[-suffix]` | Created on the release commit; the release workflow reads `<Version>` and fails if the tag doesn't match — tags do not drive the version number. |
| `## [x.y.z]` heading in [`CHANGELOG.md`](./CHANGELOG.md) | 3-segment SemVer (+ optional pre-release suffix) | Human-facing release notes, extracted verbatim into the GitHub Release body. |

CI and the release workflow both run
[`.github/scripts/verify-version-lockstep.ps1`](./.github/scripts/verify-version-lockstep.ps1),
which fails the build if `<Version>` and **any** Angular package version drift apart, or if any
`@dignite/*` dependency between those packages is not pinned to exactly that version.

### Cutting a release

**The workspace `<Version>` always equals the currently-published version — never a "next dev
version" opened in advance.** Bump it only when you are about to cut the release it names, in the
same commit as that release; do not follow a release with a separate bump commit the way many repos
do. This matters more here than in most repos: `site` (and any other repository that reaches this
one through a cross-repository `ProjectReference` rather than a published `PackageReference` — see
e.g. `Dignite.Site.slnx`'s `../../../abp-modules/...` paths) has `dotnet pack` turn that
`ProjectReference` into a `PackageReference` pinned to whatever `<Version>` sits in this workspace at
pack time, regardless of whether that version was ever actually published. A version bumped "for
next time" and left sitting in the workspace between releases is invisible until some downstream
pack picks it up as a dependency floor that doesn't exist on any feed. This already happened:
`10.0.0-rc.6` was opened here immediately after cutting `rc.5`, and `site`'s very next release build
pinned to `>= 10.0.0-rc.6` through its `ProjectReference`, before `rc.6` had ever been published —
breaking `site`'s restore.

1. Move the CHANGELOG `[Unreleased]` section to `## [x.y.z] - YYYY-MM-DD`, keeping entries grouped
   by module so readers can tell which half of the repo changed.
2. In that same commit, bump `<Version>` in `Directory.Build.props` and `version` in every Angular
   `package.json` tracked by
   [`verify-version-lockstep.ps1`](./.github/scripts/verify-version-lockstep.ps1) (currently three
   packages) to `x.y.z`, along with every `@dignite/*` dependency pin inside those files. This is
   the only time these values move; they then stay on `x.y.z` until the commit that cuts the *next*
   release.
3. Tag and push: `git tag vX.Y.Z && git push origin vX.Y.Z`. The release workflow
   (`.github/workflows/release.yml`) triggers on `v*` tags and ends by creating a **draft** GitHub
   Release from the CHANGELOG section — review and publish it by hand. `workflow_dispatch` does not
   create a GitHub Release.
4. A **stable** version on a tag publishes the NuGet packages to NuGet.org and all three Angular
   libraries to npm under the `latest` dist-tag. A **pre-release**, tagged or dispatched, goes to
   GitHub Packages only — see [Pre-releases: GitHub Packages only](#pre-releases-github-packages-only);
   a pre-release tag additionally gets a draft GitHub pre-release. `workflow_dispatch` never publishes
   to a public registry, whatever the version.
5. NuGet.org publishing uses Trusted Publishing rather than a stored API key. The NuGet.org policy
   must select the intended package owner and match GitHub repository
   `dignite-projects/abp-modules` plus workflow file `release.yml`. Set the repository variable
   `NUGET_USER` to the NuGet profile name used by `NuGet/login@v1`; never use an email address for
   this value.

Re-running `workflow_dispatch` without bumping the version first does not fail: NuGet pushes use
`--skip-duplicate`, and the GitHub Packages npm step treats an already-published version (`E409`) as a
soft skip, since that registry refuses to overwrite one. It also publishes nothing new, so it only
re-verifies the build. The same soft skip is what lets a failed pre-release tag run be re-run past
the GitHub Packages step.

> **Migrating from the old repositories:** these packages were previously released from
> `dignite-projects/abp-file-storing` and `dignite-projects/abp-notifications`. Each package's
> NuGet.org Trusted Publishing policy must be repointed to this repository and its `release.yml`,
> or the release will fail at the publishing step.
