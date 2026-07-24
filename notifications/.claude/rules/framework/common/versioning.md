# Versioning — MAJOR Tracks ABP, Not This Module's Own Breaking Changes

> This file has **no `paths:` frontmatter, so it always loads**. It exists because this repo's
> versioning scheme deviates from classic SemVer in one specific, easy-to-misread way: full
> rationale and the release procedure live in
> [`CONTRIBUTING.md`](../../../../CONTRIBUTING.md#versioning-and-releases) — read that before
> cutting a release. This file is the terse version to stop you from misinterpreting a version
> bump while just writing code.

## The one rule

`<Version>`'s **MAJOR** segment tracks the **ABP Framework major version** this release targets
(pinned in `Directory.Packages.props`) — **not** a count of this module's own breaking changes.
MINOR and PATCH are this module's own independent SemVer counters:

- **MINOR** = this module's own backward-compatible feature addition, **and also this module's
  own breaking change** (there is no separate signal for "breaking" below MAJOR under this
  scheme — read the description of any MINOR bump before assuming it's safe to pull
  automatically).
- **PATCH** = this module's own fix, no contract change.
- MINOR and PATCH reset to `.0.0` whenever the tracked ABP major changes (e.g. moving from ABP
  10.x to 11.x jumps this module to `11.0.0`, never `11.5.3`).

## Where NOT to look for "is this breaking"

Don't infer "non-breaking" from a MINOR bump the way you would in classic SemVer — under this
scheme MAJOR answers a different question ("which ABP major does this support") than the one
classic SemVer users expect it to answer ("did anything break"). Check the CHANGELOG entry, not
just the version shape.

## Mechanics

- `<Version>` lives in root `Directory.Build.props`, applies to all 15 packable projects — there
  is no per-project versioning in this repo.
- `<AssemblyVersion>` is pinned separately (`1.0.0.0`) and never bumped in lockstep with
  `<Version>` — see `notifications-invariants.md` §1 for why assembly-version churn is dangerous
  here specifically (`AssemblyQualifiedName`-based deserialization of historical
  `NotificationData`).
- First published version is a prerelease (`10.0.0-preview.1`), not `10.0.0` stable — graduating
  to stable is a deliberate, later step.
- New package version pins for *dependencies* go in `Directory.Packages.props`
  (see `framework/common/cli-commands.md`) — unrelated to this module's own `<Version>`, don't
  conflate the two when reading a diff.
