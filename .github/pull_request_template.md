### Description

Resolves #xxxx (write the related issue number if there is one)

<!-- Describe what this PR changes. Add a screenshot or animated GIF for UI changes. If it is a
     breaking change, say so and explain how existing consumers should migrate. -->

### Affected module(s)

<!-- Tick the module tree(s) this PR touches. The two modules never reference each other, so most
     PRs touch only one. -->

- [ ] `file-storing/` (Dignite.Abp.FileStoring / Dignite.FileExplorer)
- [ ] `notifications/` (Dignite.Abp.Notifications / Dignite.NotificationCenter)
- [ ] Repo-wide (build, CI, docs, shared config)

### Checklist

- [ ] I built and tested it locally (`./build/build-all-release.ps1` and `./build/test-all.ps1`, or the equivalent `dotnet` commands)
- [ ] I added or updated unit / integration tests where it made sense
- [ ] I updated the docs / `CHANGELOG.md` (or no documentation change is needed)
- [ ] I did **not** add a `ProjectReference` across the `file-storing/` ↔ `notifications/` boundary, rename a PackageId, or inline a library package version (all repo invariants — see `CLAUDE.md`)

### How to test it?

<!-- Describe how a reviewer can verify this change, or remove this section if it is already obvious. -->
