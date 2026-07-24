# Versioning — One Lockstep Version for the Whole Repo; MAJOR Tracks ABP

> This file has **no `paths:` frontmatter, so it always loads**. This repo's versioning scheme deviates from
> classic SemVer in two specific, easy-to-misread ways. The full rationale and the release procedure live in
> the repository root [`CONTRIBUTING.md`](../../../../../CONTRIBUTING.md#versioning-and-releases) — read that
> before cutting a release. This file is the terse version to stop you from misinterpreting a version bump
> while just writing code.

## The two rules

**1. One version for the entire repository.** `notifications/` and `file-storing/` are released in lockstep
from a single `<Version>` in the **repository root** `Directory.Build.props` — all 15 packable projects here,
all 10 in `file-storing/`, and both Angular packages. There is no per-module and no per-project version.

A consequence you will see in the history: a change to `file-storing/` alone still bumps the version of every
package here, with no content change. **That is expected, not a mistake** — don't "fix" it, and don't add a
module-local `<Version>` to avoid it.

**2. MAJOR tracks the ABP Framework major version** this release targets (pinned in the root
`Directory.Packages.props`, currently ABP 10.5.0) — **not** a count of this repo's own breaking changes. It
must stay **≥ 10** permanently: the legacy `Dignite.Abp.Notifications*` package line is published on NuGet up
to `3.8.2` under these same PackageIds, and MAJOR ≥ 10 is what keeps these releases winning "latest version"
resolution.

MINOR and PATCH are the repository's own counters:

- **MINOR** = a backward-compatible feature addition, **and also a breaking change** (there's no separate
  "breaking" signal below MAJOR under this scheme — read the description of any MINOR bump before assuming
  it's safe to pull automatically).
- **PATCH** = a fix, no contract change.
- MINOR and PATCH reset to `.0.0` when the tracked ABP major changes (moving from ABP 10.x to 11.x jumps to
  `11.0.0`, never `11.5.3`).

## Where NOT to look for "is this breaking"

Don't infer "non-breaking" from a MINOR bump the way you would in classic SemVer — under this scheme MAJOR
answers a different question ("which ABP major does this support") than the one classic SemVer users expect it
to answer ("did anything break"). Check the root `CHANGELOG.md` entry, not just the version shape. And because
releases are lockstep, check *which module* the entry is under — a version bump does not mean this module
changed.

## Mechanics

- `<Version>` lives in the **root** `Directory.Build.props`, not this module's. This module's
  `Directory.Build.props` is a thin file that imports the root one and overrides only `Product` /
  `PackageTags` — don't add version properties to it.
- **Keep the Angular package in step.** `angular/projects/notification-center/package.json`'s version must
  equal `<Version>`; CI and the release workflow run `.github/scripts/verify-version-lockstep.ps1`, which
  fails the build when either module's Angular package drifts from the .NET version.
- `<AssemblyVersion>` is pinned separately (`1.0.0.0`) and **never** bumped in lockstep with `<Version>`:
  without this the SDK derives `AssemblyVersion` from `<Version>`, moving it on every release and breaking
  `AssemblyQualifiedName`-based lookups. This repo treats assembly-version stability as load-bearing for
  `NotificationData` deserialization — see `notifications-invariants.md` §1. It is pinned in the **root**
  `Directory.Build.props` now; that pin is repo-wide and must not be removed there.
- The current line is a prerelease, not `10.0.0` stable — graduating to stable is a deliberate, later step.
- New package version pins for *dependencies* go in the root `Directory.Packages.props` (see
  `framework/common/cli-commands.md`) — unrelated to the repo's own `<Version>`; don't conflate the two when
  reading a diff.
