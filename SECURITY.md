# Security Policy

Covers every package released from this repository — both the `file-storing/` and `notifications/`
module trees.

## Supported versions

| Version | Supported |
| ------- | --------- |
| `10.0.x` pre-release | ✅ (pre-release; see [CONTRIBUTING → Versioning and releases](CONTRIBUTING.md#versioning-and-releases)) |

This project has not yet reached a stable release; pre-releases are supported on a best-effort
basis. Once `10.0.0` (or later) ships, this table will track the currently-supported minor line(s)
against the ABP Framework major version each targets.

Because every package in this repository is released in lockstep on a single version, a supported
version line covers both modules at once.

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Instead, use GitHub's private vulnerability reporting for this repository:

1. Go to [https://github.com/dignite-projects/abp-modules/security/advisories/new](https://github.com/dignite-projects/abp-modules/security/advisories/new)
2. Fill in the advisory form with as much detail as you can:
   - A description of the issue and its impact
   - Steps to reproduce (a minimal proof of concept helps a lot)
   - Affected package (e.g. `Dignite.Abp.FileStoring`, `Dignite.FileExplorer.HttpApi`,
     `Dignite.Abp.Notifications`, `Dignite.NotificationCenter.HttpApi`, a specific persistence
     provider) and version / commit
   - Any suggested remediation, if you have one

Reports about the demo hosts (`file-storing/host/`, `notifications/host/`) are welcome but are
triaged at lower priority: they are local-development demos, never packed or published, and not
intended to be deployed.

## What to expect

- We will acknowledge your report through the advisory thread, normally within **7 days**.
- We will assess the report, keep you informed of progress, and work with you on a fix and
  coordinated disclosure. This is a volunteer-maintained open-source project, so exact timelines
  depend on severity and maintainer availability — critical issues are prioritized.
- Once a fix is released, the advisory will be published and you will be credited (unless you
  prefer otherwise).

Please give us a reasonable opportunity to address the issue before any public disclosure.

## Dependency scanning

CI and the release workflow both run `dotnet list package --vulnerable --include-transitive` and
fail on High or Critical advisories. A small set of known advisories is allowlisted with an inline
justification in [`.github/workflows/ci.yml`](.github/workflows/ci.yml) and
[`.github/workflows/release.yml`](.github/workflows/release.yml) — read those comments before
assuming a package is unaffected, and drop an entry from the allowlist as soon as its advisory is
genuinely remediated.

**Standing item:** the allowlisted Scriban advisory (GHSA-7jvp-hj45-2f2m) reaches
`Dignite.Abp.Notifications.Emailing` and `.Emailing.Identity` — **packages this repository actually
publishes**, not just a demo host. No patched Scriban exists upstream, so the entry is an accepted
risk, not a resolved one; it still owes a real exploitability assessment or a replacement library.
The `Web.Host`-only advisories (`MessagePack`, `Microsoft.OpenApi`, `SQLitePCLRaw`) affect the
local-dev demo alone and should be closed by pinning fixed versions when they ship.
