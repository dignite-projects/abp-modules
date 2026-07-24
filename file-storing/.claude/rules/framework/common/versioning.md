# Versioning — MAJOR Tracks ABP, Not This Module's Own Breaking Changes

> This file has **no `paths:` frontmatter, so it always loads**. This repo's versioning scheme deviates from
> classic SemVer in one specific, easy-to-misread way. The short rationale lives in
> [`CONTRIBUTING.md`](../../../../CONTRIBUTING.md#versioning) — read that before cutting a release. This file
> is the terse version to stop you from misinterpreting a version bump while just writing code.

## The one rule

`<Version>`'s **MAJOR** segment tracks the **ABP Framework major version** this release targets (pinned in
`Directory.Packages.props`, currently ABP 10.5.0) — **not** a count of this module's own breaking changes.
MINOR and PATCH are this module's own independent counters:

- **MINOR** = this module's own backward-compatible feature addition, **and also its own breaking change**
  (there's no separate "breaking" signal below MAJOR under this scheme — read the description of any MINOR
  bump before assuming it's safe to pull automatically).
- **PATCH** = this module's own fix, no contract change.
- MINOR and PATCH reset to `.0.0` when the tracked ABP major changes (moving from ABP 10.x to 11.x jumps this
  module to `11.0.0`, never `11.5.3`).

## Where NOT to look for "is this breaking"

Don't infer "non-breaking" from a MINOR bump the way you would in classic SemVer — under this scheme MAJOR
answers a different question ("which ABP major does this support") than the one classic SemVer users expect it
to answer ("did anything break"). Check the `CHANGELOG.md` entry, not just the version shape.

## Mechanics

- `<Version>` lives in root `Directory.Build.props` (currently `10.0.0-rc.1`) and applies to **every library
  project** — there is no per-project versioning here. The demo `host/` is not published.
- **Keep the Angular package in step.** `angular/projects/file-explorer/package.json`'s version should match
  `<Version>` for coordinated releases — the .NET packages and the Angular package are consumed together.
- `<AssemblyVersion>` is pinned separately (`1.0.0.0`) and is **not** bumped in lockstep with `<Version>` —
  keeping the assembly version stable avoids strong-name/binding churn for consumers across releases. (Unlike
  some ABP modules, nothing here serializes CLR type identities, so there's no deserialization coupling to the
  assembly version — but keep it pinned regardless.)
- The first published version is a prerelease (`10.0.0-rc.1`), not `10.0.0` stable — graduating to stable is a
  deliberate, later step, gated on the outstanding host-integration / data-integrity work.
- New package version pins for *dependencies* go in `Directory.Packages.props` (see
  `framework/common/cli-commands.md`) — unrelated to this module's own `<Version>`; don't conflate the two when
  reading a diff.
