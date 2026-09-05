# Changelog

All notable changes to the packages released from this repository — the `file-storing/`,
`notifications/` and `flex-fields/` modules — are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project
adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html) — with two deviations from the
classic scheme: every package in the repository shares **one lockstep version**, and `MAJOR` tracks
the targeted **ABP Framework** version rather than this repository's own breaking changes. See
[CONTRIBUTING.md → Versioning and releases](CONTRIBUTING.md#versioning-and-releases) for both.

Because releases are lockstep, a version may contain changes to only one module — the other modules'
packages are still republished at that version with unchanged content. Entries are grouped by module
so it stays clear which part of the repository actually moved.

## [10.0.0-rc.16] - 2026-09-05

### Fixed

- **The single "install with Yarn Classic and check for a duplicate" verification
  `build/verify-npm-single-copy.mjs` performs for issue #211 ran only after both Angular publish
  steps in `release.yml`**, so a real duplicate reaching consumers would be reported only once the
  damage was already done - the same failure shape `v10.0.0-rc.14`'s own post-publish crash exposed:
  the check runs at all, but by the time it can fail, everything is already live on npmjs and GitHub
  Packages, and a failure there can only skip the draft GitHub Release, not stop a bad publish.
  `verify-npm-single-copy.mjs` now supports two modes. `packed` installs the five tarballs the
  workflow's own `npm pack` steps already produce, pointing each `@dignite/*` dependency at its
  tarball with a `file:` path instead of a registry range - Yarn Classic resolves a `file:`
  dependency's version from the tarball's own `package.json` and reconciles it against every other
  edge in the graph exactly as it would a registry-resolved copy, so it exercises the same
  duplicate-vs-dedupe logic without anything published yet. A new "Verify packed Angular packages
  install as a single copy each" step runs this immediately after the last pack step, with no `if:`
  guard, so both a `workflow_dispatch` preview build and a tagged release get the real gate.
  `published` is the previous behavior, kept as a lighter step after the npm publish - it still
  catches what a local tarball cannot, such as a dist-tag pointing at the wrong version, but a
  failure there is no longer the only line of defense.

- **The new `packed` mode above failed outright the first time it ran against a version that had
  never been published anywhere** (`Couldn't find any versions for "@dignite/ng.file-explorer" that
  matches "^10.0.0-rc.16"`), because it only pointed the *top-level* dependency at each tarball's
  `file:` path - a sibling package's *own* packed `package.json` (e.g.
  `flex-fields-file-explorer` depending on `flex-fields`) still declares that edge as a plain semver
  range, and Yarn Classic resolves a `file:` request and a semver-range request for the same package
  name as two independent lookups rather than reusing one to satisfy the other. It went to the npm
  registry for the range and failed, since nothing at that version exists there yet - exactly the
  case this check exists to run before. `verify-npm-single-copy.mjs`'s `packed` mode now also sets
  `resolutions` to the same five `file:` paths, forcing every occurrence of a name in the tree onto
  the local tarball regardless of what range asked for it. This doesn't weaken the check:
  `verify-version-lockstep.ps1` already rejects a drifted internal range before this step ever runs,
  so every internal `@dignite/*` range is already `^<the current version>` by the time `packed` mode
  installs.

- **This repository's own `flex-fields/angular` and `file-storing/angular` demo apps were themselves
  carrying the exact `ng-zorro-antd` duplicate `6f039ef` documented as an accepted cost of
  `@abp/ng.components` pinning that package at `~21.0.0-next.1` (i.e. `<21.1.0`): `21.3.3` at each
  workspace root, declared there only for the demo's own use, and `21.0.2` nested under
  `node_modules/@abp/ng.components/node_modules` - two module-scoped `NZ_CONFIG`/`NzConfigService`
  tokens, so a root-level `provideNzConfig()`/`provideNzI18n()` never reached the copy
  `@abp/ng.components`'s own controls (e.g. `abp-tree`) resolve.** That duplication is inherent to
  `@dignite/ng.flex-fields`'s and `@dignite/ng.file-explorer`'s published `^21.0.0` peer range - a
  real downstream host may need `21.3.x` for reasons the packages can't rule out, which is why that
  peer range is untouched - but nothing required these two *demo* apps to actually be such a host:
  they exist to exercise the published packages, not to prove a wide peer range works. `ng-zorro-antd`
  is now narrowed to `~21.0.2` in both `flex-fields/angular/package.json` and
  `file-storing/angular/package.json` - inside `@abp/ng.components`'s ceiling, and the same version
  that was already nested - which collapses both workspaces back to a single copy. Both demo apps'
  production builds (`yarn build:prod`) pass unchanged at the older version. A new
  `build/check-angular-package-duplicates.mjs`, wired into `ci.yml` immediately after each of the
  three Angular workspaces' `yarn install --frozen-lockfile` steps, generalizes
  `verify-npm-single-copy.mjs`'s duplicate check from the five `@dignite/*` packages it covers to any
  bare-or-scoped target list - `ng-zorro-antd` and `@angular/cdk` for now, the latter reaching these
  same packages by the same `@abp/ng.components` route even though no version conflict currently
  splits it. `notifications/angular` is checked too even though it declares neither package itself:
  it depends on `@abp/ng.components`, which pulls both in transitively, so it is exposed to the same
  failure mode the moment something else in that workspace narrows either range. Like its sibling
  script, a target that matches nothing installed fails the run rather than passing vacuously - a
  typo'd target or a check pointed at the wrong `node_modules` is otherwise indistinguishable from a
  clean tree.

### Added

#### flex-fields

- **Two new built-in field types, `Matrix` and `Table` — the first *composite* ones, whose
  configuration declares whole field definitions inline.** `Matrix` is a repeatable list of
  polymorphic blocks (the admin declares named block types up front, each with its own sub-fields);
  `Table` is a homogeneous grid over one shared column schema. Both were written and proven in
  Dignite.Site's `Dignite.FlexFields.Site` and are ported here **with the wire format unchanged** —
  registration keys `Matrix`/`Table`, configuration keys `Matrix.BlockTypes`/`Table.Columns`, and
  camelCase `{blockTypeName, values}` / `{values}` value arrays — so fields already stored against
  the Site implementation keep working as-is. They ship as built-ins rather than a bolt-on package
  because, unlike `FileExplorer` or `CKEditor`, they depend on nothing outside the kernel's own
  vocabulary; what made them worth moving is that the two contracts below have to be answerable
  without knowing either concrete type.
  - `Dignite.Abp.FlexFields.Abstractions` gains `MatrixFieldType`/`TableFieldType` and their
    configuration types, plus four kernel contracts they share: **`ICompositeFieldType`**
    (`GetInlineFields`, so a host can ask "does this type contain other fields, and which" without
    naming a concrete type — an interface rather than an `IsComposite` bool, because every caller
    that asks also has to walk those fields), **`INormalizesValue`** (`Normalize`, the canonical wire
    shape — deliberately *not* folded into `Validate`, which returns only errors and never the parsed
    value, so a value with the wrong key casing would otherwise validate cleanly and then be stored
    verbatim and be unreadable to every camelCase reader downstream), **`InlineFieldDefinition`**
    (one inline field; carries `Required`, which a `FlexFieldData` cannot), and
    **`CompositeFieldNesting`** (`MaxDepth = 3` and the bounded measurement that enforces it — a
    configuration is a tree of unbounded depth and every reader of it recurses, so it is capped once
    on write instead of guarded in each reader).
  - `Dignite.Abp.FlexFields.Web` gains `Views/Shared/FlexFields/Matrix.cshtml` and `Table.cshtml`,
    which recurse through the existing `<flex-field-view>` dispatch for each sub-field rather than
    re-implementing rendering per type. No `Search/` partials: both types have
    `IndexValueType == null` — a list of composite objects has no typed index column to decompose
    into — so neither can be marked `Searchable`.
  - `@dignite/ng.flex-fields` gains the matching config / control / view components
    (`ff-matrix-config|control|view`, `ff-table-config|control|view`), registered in
    `BUILT_IN_FIELD_TYPES`, so an existing `provideFlexFields()` call already covers them.
    `FieldTypeDefinition` gains an optional **`composite`** flag, which Matrix and Table set and the
    config editors use to stop offering composite types once the nesting limit is reached; the
    server's `CompositeFieldNesting` remains the authority, that mirror is a courtesy.
  - Neither contract is invoked by the kernel — a host calls them, and the demo now shows both:
    `ProductAppService` normalizes the value bag before validating and saving, and
    `ProductFieldAppService` refuses a too-deeply-nested configuration on create and update.
  - **Localization moved with them**: the `FieldType:Matrix`/`FieldType:Table`, `Matrix:*`, `Table:*`
    and `Validate:Matrix:*`/`Validate:Table:*` texts now live in the `FlexFields` resource
    (`Dignite.Abp.FlexFields.Abstractions`) instead of Site's own `FlexFieldsSite` resource, in all
    four shipped cultures (`en`, `ja`, `zh-Hans`, `zh-Hant`). Three general validation keys the
    Angular side's shared error-message helper needs came along with them: **`Validate:MinValue`**,
    **`Validate:MaxValue`** and **`Validate:MaxLength`**.

## [10.0.0-rc.15] - 2026-09-05

### Fixed

- **The post-publish "Verify published packages install as a single copy each" step crashed on the
  same `${NODE_AUTH_TOKEN}` placeholder that had broken every yarn command earlier in the job**, so
  the `v10.0.0-rc.14` run failed *after* it had already published all 33 NuGet packages and all five
  Angular packages: the draft GitHub Release was never created, and the single-copy check the step
  exists for never actually ran. `v10.0.0-rc.13`'s fix moved `actions/setup-node`'s `registry-url`
  onto a second `setup-node` call placed past the last yarn command in the job - but this step runs
  *after* publishing, and therefore after that second call, whose generated `.npmrc` stays exported
  as `$NPM_CONFIG_USERCONFIG` for every remaining step. Yarn Classic expands every env-var
  placeholder in its resolved config on every invocation and throws when one is unset, and nothing
  sets `NODE_AUTH_TOKEN` (npm Trusted Publishing does not use it). The step now runs with an empty
  `NPM_CONFIG_USERCONFIG` of its own, which is all it needs: it installs published, public packages
  from npmjs and authenticates nothing. Verified by hand against the published `10.0.0-rc.14` set -
  Yarn Classic resolves exactly one copy of each of the five packages.

- **`@dignite/ng.flex-fields` and `@dignite/ng.file-explorer` imported `@abp/ng.components/tree` while
  declaring `@abp/ng.components` a peer dependency, so on any install that does not resolve peers the
  package was simply absent and the published bundles carried an import that resolved to nothing.** It
  surfaced downstream as `Could not resolve "@abp/ng.components/tree"` against both packages' `fesm2022`
  output while bundling `@dignite/ng.site` - at which point both were already published. A peer
  dependency is a claim that *the consumer already has this*, and `@abp/ng.components` is not something
  an ABP Angular host is guaranteed to have: neither `@abp/ng.core` nor `@abp/ng.theme.shared` depends
  on it, only feature packages such as `@abp/ng.identity` and `@abp/ng.setting-management` do. So a host
  that used neither had no reason to have it, and `--legacy-peer-deps` - which this repository's own
  workspaces and every known downstream need, because `@abp/ng.theme.shared`'s `@swimlane/ngx-datatable`
  caps its Angular peer at 20 - guaranteed it would not be installed even where the peer was declared.
  It is now a real `dependency` of both packages, listed in each `ng-package.json`'s
  `allowedNonPeerDependencies` so ng-packagr accepts a bundled non-peer edge as deliberate. The rule
  this restores: a package belongs in `peerDependencies` only when the consumer is guaranteed to have
  it already; otherwise it is a `dependency`.

  `ng-zorro-antd` and `@angular/cdk` reach these packages by the same single route - through
  `@abp/ng.components`, which depends on `ng-zorro-antd`, which depends on `@angular/cdk` - and by that
  rule they are irregular in the same way. They are **deliberately left as peers**. `@abp/ng.components`
  pins `ng-zorro-antd` at `~21.0.0-next.1`, that is `<21.1.0`, while these packages declare `^21.0.0`
  and current hosts run `21.3.3`; there is no version that satisfies both, so a resolver settles it by
  installing both copies. That is already the state of this repository's own workspaces - `21.3.3` at
  the root, `21.0.2` nested under `@abp/ng.components` - and it is a consequence of that upstream pin,
  not of how these packages declare anything. Two copies of ng-zorro are two module-scoped
  `NZ_CONFIG` / `NzConfigService` injection tokens, the identity split described for
  `@dignite/ng.flex-fields` in [#211](https://github.com/dignite-projects/abp-modules/issues/211):
  `provideNzConfig()` and `provideNzI18n()` at the root configure the copy these packages' own controls
  use, and not the one ABP's `abp-tree` sees. Moving them to `dependencies` would not merge the copies,
  only move the choice of the root one away from the host - so the host keeps it, and both READMEs now
  state that these two must be declared by the consumer and what a range wider than `<21.1.0` costs.
  `@abp/ng.components` had no such conflict - one range, one source - which is what makes moving that
  one a pure gain.

## [10.0.0-rc.14] - 2026-09-04

### Fixed

- **Every tag-triggered release since the npm Trusted Publishing migration (`v10.0.0-rc.12` and
  `v10.0.0-rc.13`, twice for the latter) crashed inside `release.yml` before the job ever published
  anything to NuGet.org or npm.** The job's "Setup Node.js" step set `registry-url` (needed for the
  later OIDC-based npm publish steps), which makes `actions/setup-node@v7` write an
  `//registry.npmjs.org/:_authToken=${NODE_AUTH_TOKEN}` placeholder into a generated `.npmrc`.
  Nothing in the job has set `NODE_AUTH_TOKEN` since npm publishing became OIDC-based — npm's own
  Trusted Publishing doesn't use it at all — but Yarn Classic (1.x, used by all three Angular
  workspaces) eagerly substitutes every env-var placeholder in its resolved config on **every**
  invocation, not just registry-touching ones, and throws when one is unset. A first fix dropped the
  `cache: yarn` step option that was triggering this during its internal `yarn cache dir` probe; that
  only moved the crash to the next yarn command Setup Node's cache change didn't touch — the job's
  very first real `yarn install` — because the placeholder-bearing `.npmrc` stays active for every
  step for the rest of the job, not just the one that wrote it. The actual fix moves `registry-url`
  off the early "Setup Node.js" step entirely, onto a second `actions/setup-node@v7` call added right
  after the last yarn command in the job (before the GitHub Packages and npmjs publish steps that
  need it) — so no yarn command ever sees that `.npmrc` in the first place. `ci.yml`'s
  identical-looking "Setup Node.js" step never hit any of this because it never sets `registry-url`.
- **`release.yml`'s "Publish pre-release Angular packages to GitHub Packages" step had no `--tag`
  on its `npm publish` call**, and npm refuses to publish a pre-release version (every version this
  repository has ever shipped so far) without one explicitly stated. Unlike the crash above, this
  one only broke once the job got far enough to actually reach it. Added the same
  `--tag '${{ steps.channel.outputs.npm-tag }}'` the sibling npmjs-publishing step already used.
- **The GitHub Packages pre-release step appended its own registry/auth lines to the same
  `.npmrc` `actions/setup-node`'s `registry-url` wrote for npmjs.org OIDC** (`$NPM_CONFIG_USERCONFIG`,
  read by "Publish tagged Angular packages to npm" later in the same job) — corrupting npm's parsing
  of that shared file and breaking OIDC's registry detection there with a garbled `ENEEDAUTH` error
  quoting both registries concatenated together. The GitHub Packages step now writes its auth to its
  own scratch file, passed via `--userconfig` on just its own `npm publish` calls, leaving the
  OIDC-relevant file untouched for the rest of the job.

- **The two Angular adapter packages declared their intra-repo siblings at a stale range, so
  consumers could end up with two copies of `@dignite/ng.flex-fields` and no working field types.**
  `flex-fields-ckeditor` and `flex-fields-file-explorer` both still asked for `^10.0.0-rc.4` while
  every package in the repository shipped `10.0.0-rc.13`. That range admits an older sibling, so a
  resolver is free to satisfy it with one rather than deduplicating against the copy already at the
  root — and Yarn Classic does exactly that whenever npm's `latest` tag sits on an older version
  than the newest published one, which is the case today (`latest` is `10.0.0-rc.11`, `next` is
  `10.0.0-rc.13`). The result is not wasted bytes: Angular DI keys off object identity and
  `FLEX_FIELD_TYPES` is a module-scoped `InjectionToken`, so two copies are two distinct DI keys —
  `provideCKEditorFieldType()` registers into one while `FieldTypeResolver` reads the other, and
  every field type appears unregistered at runtime with nothing having failed at install or build
  time. All three ranges now track the release version, and
  `.github/scripts/verify-version-lockstep.ps1` — already the release's version-lockstep gate —
  additionally fails the release if any `@dignite/*` dependency or peer dependency of a published
  Angular package names anything other than the version being released, so this cannot drift again.
  Note that clearing the duplicate for consumers still on `latest` also needs the `latest` dist-tag
  moved off `10.0.0-rc.11`. See [#211](https://github.com/dignite-projects/abp-modules/issues/211).
- **`release.yml` now verifies, after publishing, that a Yarn Classic install of the just-released
  packages resolves exactly one copy of each.** The packed-tarball smoke tests already in the
  workflow install with **npm**, which deduplicates; all three Angular workspaces here and every
  known downstream use **Yarn Classic**, which does not. That gap is what let the duplicate above
  reach consumers with every existing check passing. `build/verify-npm-single-copy.mjs` closes it by
  checking the *resolved* outcome rather than the manifests — so a duplicate arriving by some other
  route (a transitive `@dignite/*` edge, a dist-tag that makes a caret resolve backwards) is caught
  too. It runs after the npm publish step, since it resolves real versions from npmjs that do not
  exist until then; a failure therefore cannot un-publish anything, it stops the draft GitHub Release
  and reports that the just-published set does not install cleanly.
- **A pre-release now also takes npm's `latest` dist-tag, for as long as no stable release exists.**
  `release.yml` published every pre-release under `next` alone and reserved `latest` for a stable
  version — of which there is none yet on the 10.x line. `latest` was therefore left wherever it
  happened to land before that convention took hold (`10.0.0-rc.11`, while `10.0.0-rc.13` was the
  newest published), so a bare `npm install @dignite/ng.flex-fields` handed out a version two releases
  behind, and Yarn Classic resolved intra-repo ranges backwards onto it — the second of the three
  conditions behind [#211](https://github.com/dignite-projects/abp-modules/issues/211). The tag had
  been corrected by hand, but the workflow would have re-created the gap at the next pre-release.
  `build/resolve-npm-dist-tag.mjs` now decides it: a stable version always takes `latest`, and a
  pre-release takes it too **only while the registry holds no stable release of these packages**,
  falling back to `next` from the moment one exists. Reading the registry rather than flipping a flag
  means the rule retires itself when `10.0.0` ships, instead of silently moving consumers off a stable
  release onto a later `10.1.0-rc.1`. The `channel` output is unchanged and still means "is this a
  pre-release" for the GitHub Packages mirror and the draft Release's own flag.

#### flex-fields

- **`@dignite/ng.flex-fields`' `ng-zorro-antd` peer range rejected every release after `21.0.x`.** It
  was `~21.0.0-next.1`, which expands to `>=21.0.0-next.1 <21.1.0-0` — so `21.1.0` and everything
  since, up to the current `21.3.3`, failed the peer. Under npm 7+ that is an `ERESOLVE` install
  error rather than a warning; Yarn Classic downgrades it to a warning, which is why it had not
  surfaced. The range dated from when `21.0.0-next.x` was the newest thing published and was never
  revisited after `21.0.0` went stable. It is now `^21.0.0`, matching how `.github/dependabot.yml`
  already reasons about this package (its major tracks Angular's, so majors are ignored and the
  `21.x` line is meant to be tracked). `flex-fields/angular`'s own dependency moves to `^21.3.3`
  alongside it, so the workspace develops against a version the library claims to support — it had
  been pinned to the same capped range and stuck on `21.0.2` while `file-storing/angular` was already
  on `21.3.3`. See [#220](https://github.com/dignite-projects/abp-modules/issues/220).

- **`CKEditorControlComponent`'s theme bridge now resolves against `<body>` as well as `<html>`.** A
  custom property's `var()` references are resolved against the element the property is declared on,
  not against wherever it is eventually consumed, so the bridge's `:root` declarations could only
  ever see theme variables set on `<html>`. A host that marks its dark theme on `<body>` instead —
  `data-bs-theme="dark"` on the body element, say — left the bridge resolving the light values, and
  the editor stayed light while the rest of the page went dark. Both blocks are now declared on
  `:root, body`, so either placement works. Coverage is unchanged: CKEditor's UI, including the
  balloon/dropdown wrapper it appends directly under `<body>`, is entirely inside the body subtree.
- **The `Select` and `Tree` field controls’ dropdown panels now follow a dark host.** Both painted
  their panel with `var(--lpx-content-bg, #fff)`, but `--lpx-content-bg` is a **full** LeptonX token
  (`@volosoft/ngx-lepton-x`: `#f0f4f7` light, `#121212` dark). LeptonX **Lite** — `@volo/ngx-lepton-x.lite`,
  which `@abp/ng.theme.lepton-x` wraps — never defines it: it ships 11 `--lpx-*` tokens and this is
  not one of them. The chain therefore fell straight through to the literal `#fff` and the panel
  stayed white in every theme, while the options’ own `color: var(--bs-body-color)` did follow the
  host — light grey text on white, unreadable. Both now fall back to `--bs-secondary-bg` first
  (`#e9ecef` light, `#343a40` dark in Lite), the Bootstrap 5.3 “one step off the body surface” token
  that every Bootstrap-based theme defines at `:root` and redefines under `[data-bs-theme=dark]` —
  the same chain shape `flex-fields-ckeditor` already uses for `--ck-color-base-foreground`. Light
  mode moves from pure white to `#e9ecef`.
- **The `Select` field's multi-select tags were near-illegible in a dark host.** ng-zorro hardcodes
  the tag's entire chrome — `background: #f5f5f5`, `border: 1px solid #f0f0f0`, and
  `rgba(0, 0, 0, 0.45)` on the remove icon — while the tag's label does follow the host, because this
  file already sets `color: inherit` on `.ant-select`. The result in dark mode was a light label on a
  near-white chip with an invisible “×”. All three now map to `--bs-secondary-bg`,
  `--bs-border-color` and `--bs-secondary-color`.

- **The `Tree` field gave no feedback about which node was selected in single-select mode.**
  `abp-tree` marks selection with a `.selected` class of its own — it renders
  `<div [class.selected]="isNodeSelected(node)">` as the node wrapper's direct child — and never binds
  `nzSelectedKeys`, so ng-zorro's `.ant-tree-node-selected` is never applied at all, and `abp-tree`
  attaches no styling to `.selected`. Clicking a node therefore changed nothing on screen, and
  reopening a saved value gave no indication of which node it held. (Multi-select was unaffected: it
  renders a checkbox per node.) The selected node now takes the same `--lpx-brand` / white treatment
  as the `Select` field's chosen option. Node hover, which neither package had touched and which
  ng-zorro paints `#f5f5f5`, now uses `--bs-secondary-bg`.

- **The `Tree` field's search dropdown had a hardcoded `rgba(0, 0, 0, 0.12)` border**, invisible
  against the dark panel. Now `--bs-border-color`, like every other Bootstrap-based control.
- **The `Tree` field's node editor (`ff-tree-config`, the “Nodes” panel in field configuration)
  carried no styles of its own**, so every ng-zorro default came through unmodified — all of it
  hardcoded light-mode. Hovering a node painted a `#f5f5f5` bar under text that follows the host
  (`abp-tree` sets `.ant-tree { color: inherit }`), so in a dark host the row went light-on-white and
  the label disappeared; the post-click “active” node did the same. `.ant-tree`'s own opaque
  `background: #fff` was there too, which would have shown the whole editor as a white box in a
  genuinely dark host. Hover and active now use `--bs-secondary-bg` and the tree background is
  transparent, matching what the `Tree` picker already did.

- **A selected option in the `Select` field's dropdown rendered white text on the panel background
  once the mouse left it.** The rule meant to give selected options a `--lpx-brand` fill carried
  `!important` on its colour but not on its background, while the `.ant-select-item` rule below it
  zeroes every option background with `!important` — and `!important` beats specificity, so the
  background never applied and the white text always did. It was invisible for as long as the panel
  was `#fff` and became merely illegible once the panel started following `--bs-secondary-bg`. The
  hover rule has `!important` on both halves, which is why hovering a selected option looked correct
  and moving off it did not. Selected options now take no colour of their own and read exactly like
  unselected ones, with ant-design's own checkmark as the indicator: in a multi-select several
  options are selected at once, and filling each of their rows competes with the hover state rather
  than adding information.
## [10.0.0-rc.13] - 2026-09-03

### Added

- **`build/check-angular-package-deps.mjs`, a CI gate that fails when a built library imports a
  package its own `package.json` does not declare.** ng-packagr marks every bare specifier it does
  not bundle as an external but never checks that the external is declared, so a library could
  publish a bundle asking for a package it never named — nothing failed at build time, nothing
  failed at `npm install`, and the consumer met an unresolvable specifier the first time they built
  their own app. The existing `smoke-test-angular-package.mjs` cannot see this class of defect: it
  seeds the throwaway consumer with the demo app's dependency list, so every undeclared package is
  already installed before the compile it verifies. All five libraries were failing this check when
  it was written.

### Changed

- npmjs publishing switched from a long-lived `NPM_TOKEN` secret to npm Trusted Publishing (OIDC);
  each of the five Angular packages now has its own Trusted Publisher configured on npmjs.com.
  `@dignite/ng.flex-fields-ckeditor` is published to npmjs on tagged releases for the first time.
- The GitHub Packages pre-release npm mirror now tolerates re-runs at an unchanged version,
  treating "already published" as a skip instead of failing the step.
- **Dependencies a consumer cannot already have moved from `peerDependencies` to `dependencies`.**
  Every ABP 10.5 + Angular 21 host is forced to set `legacy-peer-deps` — `@abp/ng.theme.shared`
  pins `@swimlane/ngx-datatable@~22.0.0`, whose Angular peer range stops at 20 — and under that
  flag npm does not install peer dependencies at all. A peer the host does not already have is
  therefore an unresolvable import discovered at the consumer's build, with nothing in
  `npm install` to warn them. The evidence that the peer model never worked here: all five known
  consumers (`site`, `vault-extract`, and this repository's own three demo apps) had hand-copied
  the same peer lists into their own `package.json`, at three different `ng-zorro-antd` ranges.
  Moved to `dependencies`: `ckeditor5`, `@ckeditor/ckeditor5-angular`, `marked` and
  `@dignite/ng.flex-fields` in `@dignite/ng.flex-fields-ckeditor`; `@dignite/ng.flex-fields` and
  `@dignite/ng.file-explorer` in `@dignite/ng.flex-fields-file-explorer`; `@microsoft/signalr` in
  `@dignite/ng.notification-center`. Packages an ABP Angular host has by construction
  (`@angular/*`, `rxjs`, `@abp/ng.*`) or through ABP's own dependency tree (`@ngx-validate/core`,
  `ng-zorro-antd`, `@angular/cdk`, `@ng-bootstrap/ng-bootstrap`, `@swimlane/ngx-datatable`) stay
  peer dependencies: there the declaration states the tested range without risking a second copy
  of a singleton in the consumer's tree. Consumers that were carrying those packages by hand can
  drop them; consumers that were not no longer have to discover them.

### Fixed

- **Every Angular package imported at least one package it did not declare.**
  `@ngx-validate/core` in `@dignite/ng.flex-fields`, `@dignite/ng.flex-fields-ckeditor` and
  `@dignite/ng.flex-fields-file-explorer`; `rxjs` in `@dignite/ng.flex-fields-ckeditor` and
  `@dignite/ng.notification-center`; `@swimlane/ngx-datatable` in `@dignite/ng.file-explorer`; and
  `@ckeditor/ckeditor5-integrations-common`, whose `EditorRelaxedConstructor` appears in
  `@dignite/ng.flex-fields-ckeditor`'s published `.d.ts`, so a consumer compiling without
  `skipLibCheck` needed it resolvable. All now declared; the new dependency check keeps them so.
- **`flex-fields-ckeditor` was built and published by `release.yml` but never built by `ci.yml`**,
  so a pull request that broke the CKEditor adapter stayed green until tag time. CI now builds it
  alongside the other libraries.

#### flex-fields

- **`CKEditorControlComponent` now follows the host theme, including dark mode.** CKEditor 5 ships a
  single stock light palette (`--ck-color-base-background`/`-foreground`/`-border`/`-text` in its own
  `:root`); the control set none of them, so in a dark-themed host the editor stayed light unless the
  host added its own bridge (only `site` had one). The control now maps those four tokens, plus the
  two hardcoded toolbar-button hover/active fills, to the host's theme variables — a LeptonX token
  when present, falling back to the Bootstrap 5.3 token every ABP Angular theme ships, then to
  CKEditor's stock literal — so a host on full LeptonX (`@volosoft/ngx-lepton-x`) or on a plain
  Bootstrap 5.3 dark theme no longer needs a `--ck-color-base-*` bridge of its own. Two limits are
  worth knowing. The editor's main chrome — toolbar, balloon and dropdown panels, list and input
  surfaces — is `var()`-derived from those four tokens and re-themes with them, but roughly 60 other
  `--ck-color-*` tokens are hardcoded literals in `ckeditor5.css` and stay light regardless. And
  LeptonX **Lite** (`@volo/ngx-lepton-x.lite`, which `@abp/ng.theme.lepton-x` wraps) ships no dark
  theme at all — one fixed look, no theme-switching code in the package — and pins `--lpx-card-bg` to
  a constant `#ffffff`, so on Lite the editor stays white, matching Lite's own white cards. A host
  hand-rolling a dark mode on top of Lite has to override `--lpx-card-bg` itself.

## [10.0.0-rc.11] - 2026-08-31

### Fixed

#### flex-fields

- **`NumberControlComponent` no longer changes its control's value type when it truncates.** Typing more
  decimal places than `Number.Decimals` allows made `onInput` write the truncated value back as a
  **string**, while every other keystroke leaves Angular's own `NumberValueAccessor` value — a number — in
  place. A consumer serializing the form straight to JSON therefore sent `"1.23"` where its contract said
  number, but only for the values that overflowed the configured precision, which made it look
  intermittent. The truncated value is now patched as a number; a non-finite result (an empty or
  lone-sign integer part) still falls back to the raw string rather than silently becoming `0`.

## [10.0.0-rc.7] - 2026-08-30

> `10.0.0-rc.6` also exists on NuGet.org with identical package contents — that release run failed
> at the npm publishing step (the `NPM_TOKEN` granular access token had no package/scope
> permissions), so none of the four public Angular packages were ever published under it. `rc.7`'s
> own tagged run (and `rc.11`'s) also failed at that step, this time because the replacement token
> still required a 2FA one-time password — so no `10.0.0-rc.*` Angular package after
> `@dignite/ng.notification-center@10.0.0-rc.3` reached npmjs from CI. The `rc.11` tarballs are
> published manually out-of-band, and CI moves to npm Trusted Publishing going forward.

### Changed

#### flex-fields

- **Breaking: `IFlexField.Description` no longer has a length cap.** `FlexFieldConsts.MaxDescriptionLength`
  (256 characters) is removed; the EF Core column now maps to the provider's unbounded text type instead
  of a fixed-width one. `Description` is sometimes used as free-form prompt text for an AI rather than a
  short blurb, which routinely exceeded the previous cap. Hosts add their own migration to widen the
  column next time they touch the model.

#### file-storing

- `Dignite.FileExplorer.HttpApi.Client` now registers `AddStaticHttpClientProxies` instead of the unused
  dynamic `AddHttpClientProxies` path — the generated `ClientProxies/` code already shipped but wasn't
  being used.

#### notifications

- `Dignite.NotificationCenter.HttpApi.Client` switches from dynamic to static proxies
  (`AddStaticHttpClientProxies`), gaining generated `ClientProxies/` code and a new
  `NotificationCenterRemoteServiceConsts` type (mirroring `FileExplorerRemoteServiceConsts`) so the
  controllers' `RemoteService`/`Area` names and the client module's `RemoteServiceName` reference one
  constant instead of matching hardcoded strings by hand.

### Fixed

#### flex-fields

- CKEditor's editable content now themes correctly in dark mode — `--ck-content-font-color` is
  repointed at the same base-text token the control already sets, instead of inheriting CKEditor 5's
  own light-mode default.
- The Select field type's dropdown options are now readable in dark mode — an ng-zorro CSS specificity
  tie was leaving idle (non-hover, non-selected) options on ng-zorro's own light-mode colors regardless
  of theme.
- The Tree field type's picker (inline control and search dropdown) no longer paints an opaque white
  background in dark mode.
- `ConfigureAwait.Fody` is now actually applied to the CKEditor and FileExplorer integration projects —
  see file-storing below for why this matters.

#### file-storing

- `ConfigureAwait.Fody` is now actually applied to the core and file-explorer projects — they were
  missing the per-project `FodyWeavers.xml` opt-in file the repo-wide convention requires, so
  `.ConfigureAwait(false)` wasn't being woven into their `await`s. A host running these modules on a
  `SynchronizationContext` (WPF/WinForms/Blazor Server/classic ASP.NET) could previously deadlock.

## [10.0.0-rc.5] - 2026-08-17

### Added

#### flex-fields

- **New Angular package `@dignite/ng.flex-fields`.** The Angular half of FlexFields, migrated from
  `Dignite.Abp.DynamicForms`' `@dignite-ng/expand.dynamic-form` in the `dignite-abp` repository and
  renamed in step with the C# naming map. Ships config / control / view / search components for all
  six field types, the `FieldTypeResolver` registry, and `provideFlexFields()`.
  - The six **registration keys** (`Text`, `Number`, `DateTime`, `Select`, `Boolean`, `Tree`) and
    every configuration key are stored values, not class names, and match the server byte-for-byte —
    a test asserts each one. These were renamed once more during development, from the pre-rename
    `Dignite.Abp.DynamicForms` stragglers (`TextEdit`/`NumericEdit`/`DateEdit`/`Switch`/`TreeView`) to
    align with the C# type names, before this package's first release — see `flex-fields/CLAUDE.md`
    for the full mapping.
  - Registering a field type is now a typed `InjectionToken` multi-provider instead of a bare
    `'MERGED_FORM_CONFIG'` string token that the library never provided and a `forRoot()` that
    silently discarded its argument. Several packages can register independently, and a later
    registration overrides a built-in of the same name.
  - Components are standalone; the `dynamic-form` NgModule is gone.
  - The stale embedded copy of the FileExplorer API proxy was dropped — it was dead code, and it
    coupled two modules that are meant to be independently installable.
  - The tree designer's value suggestion is now an optional `FLEX_FIELD_SLUG_GENERATOR` token with a
    plain slug default, rather than a hard-wired `pinyin-pro` dependency in a general-purpose module.
  - `DateTime` still has no search component, as before. Known gap, not a regression.
  - `Dignite.Abp.FlexFields.Abstractions`'s `FlexFields` localization resource gains two keys the
    migrated tree designer's node-key validator needs: `Validate:InvalidNodeValue`,
    `Validate:NodeValueAlreadyExists` (en/ja/zh-Hans/zh-Hant).
- **New `Dignite.Abp.FlexFields.Web` package.** `<flex-field-view>`/`<flex-field-search>` TagHelpers
  plus default views for the six built-in field types — the server-side (Razor) counterpart to the
  Angular library's `<ff-flex-field-view>`/`<ff-flex-field-search>`. Zero-IO by design: TagHelpers
  render an already-resolved `FlexFieldValue`, the same way the kernel has no application service of
  its own to look one up with, so assembling it is the host's job. No config/control TagHelpers —
  display and search only. `<flex-field-search>` renders inputs only; turning what gets submitted into
  a `FlexFieldQueryCondition` stays the host's job, same as the Angular library.
- **New `Dignite.Abp.FlexFields.FileExplorer.Web` package.** The FileExplorer bolt-on field type's own
  view — file name/size/MIME type/link, read straight out of the value the Angular picker already
  denormalized at pick time. No IO, no reference to `Dignite.FileExplorer`, and no search partial
  (`FileExplorerFieldType.IndexValueType` is `null`). Depending on it alone pulls in both the field
  type and `Dignite.Abp.FlexFields.Web`.

### Changed

- **Repository merged.** `dignite-projects/abp-file-storing` and `dignite-projects/abp-notifications`
  are now developed and released together from `dignite-projects/abp-modules`, as `file-storing/`
  and `notifications/`. **No PackageId, root namespace, or `AssemblyVersion` changed** — this is
  transparent to NuGet and npm consumers. Both modules' full commit histories are preserved.
- **Versioning is now lockstep across both modules** (previously each repository versioned
  independently): one `<Version>` in the root `Directory.Build.props`, one `v*` tag, one release
  pipeline covering all 25 NuGet packages and both Angular packages.
- `PackageProjectUrl` / `RepositoryUrl` and both Angular packages' `homepage` / `repository.url` now
  point at `dignite-projects/abp-modules`.

#### file-storing

- The Angular package `@dignite-ng/expand.file-explorer` jumps from `10.0.0-rc.1` to the lockstep
  version, catching it up to the .NET packages it ships alongside.
- **Breaking: the Angular package is renamed `@dignite-ng/expand.file-explorer` →
  `@dignite/ng.file-explorer`**, matching `@dignite/ng.notification-center` and
  `@dignite/ng.flex-fields` so all three npm packages share one convention. Both entry points move
  (`@dignite/ng.file-explorer` and `@dignite/ng.file-explorer/config`); nothing else about the
  package changed. Update your imports and `package.json`. The old name is not deprecated-with-a-
  shim, it simply stops receiving updates — acceptable because the only versions ever published
  under it are `10.0.0-rc.*` pre-releases.

---

> **History before the merge.** The releases below were published from the standalone
> `dignite-projects/abp-notifications` repository and cover the **`notifications/` module only**.
> The `file-storing/` module had not published a release at the time of the merge, so it has no
> entries before this point.

## [10.0.0-rc.3] - 2026-07-23

> This pre-stable line explored a much larger feature surface (per-recipient delivery reliability with leases /
> retries / dead-lettering / force-delivery, per-user delivery preferences + quiet hours, large-audience broadcast
> orchestration, payload schema-versioning + upcasters, opt-in definition payload/entity contracts, a replaceable
> batch eligibility evaluator + trusted-recipient bypass API, distribution metrics, a prepared multi-job
> fan-out, and a scheduled retention/lifecycle-cleanup worker + options) and then **cut all of it before release**
> as over-engineering for a best-effort in-app notification module. None of it shipped, so it is not documented as removed below. The module's positioning is deliberately
> "best-effort in-app notifications": delivery is fire-once (the inbox row is authoritative), and distributed-systems
> machinery (delivery reliability/retries, broadcast jobs, schema-evolution upcasters) is intentionally absent.

### Added

- MongoDB integration with ABP's distributed event outbox and inbox through `UseNotificationCenterMongoDbOutbox()`,
  using ABP-compatible event-box collections with query indexes and sharing atomic commit/rollback tests with EF
  Core. The transactional outbox requires a transaction-capable MongoDB 4.0+ replica set and transactional ABP
  units of work.
- Scoped subscription application/REST contracts that round-trip the stable entity type and ID, while retaining the
  name-only methods as definition-wide compatibility wrappers. MVC and Angular subscription UIs submit the complete
  scope.
- Tolerant notification-data reads: `INotificationDataSerializer.Deserialize(json)` returns a safe
  `UnsupportedNotificationData` placeholder for unknown or malformed payloads instead of throwing, so one bad
  historical row cannot fail a whole inbox page. MVC and Angular render it with a localized fallback. (A
  strict-vs-tolerant read-mode switch existed briefly pre-release; it was cut because every real read boundary
  already chose tolerant, so strict mode had no caller. Not documented as removed below, per the note above.)
- A bounded recipient pipeline: `INotificationStore.GetSubscriptionUserIdsAsync` keyset paging plus bounded inbox
  multi-insert. Explicit fan-outs above `NotificationDistributionOptions.DirectDistributionUserThreshold` run on a
  single background job whose distributor batches recipients internally (`RecipientBatchSize`).

### Changed

- **Breaking NotificationCenter package-family rename before 10.0.0 stable.** The optional Notification Center
  packages, namespaces, and module class names now use `Dignite.NotificationCenter*` instead of
  `Dignite.Abp.NotificationCenter*`. This is a naming-only change with no functional behavior change.
- **Breaking application/domain API alignment before 10.0.0 stable.** Current-user inbox services are now
  `IUserNotificationAppService` / `UserNotificationAppService`, and `GetCountAsync` is `GetNotificationCountAsync`.
  Pass-through manager interfaces and `UserNotificationManager` were removed; application reads now use
  `INotificationStore` while the concrete `NotificationSubscriptionManager` owns validated subscription mutation.
  REST routes are unchanged.
- **Breaking options split before 10.0.0 stable.** The catch-all `NotificationOptions` type was replaced by
  `NotificationDefinitionRegistration` (provider registration) and `NotificationDistributionOptions` (inline/background
  threshold + `RecipientBatchSize`, capped by `MaxBatchSize` = 10,000, validated on startup). Custom constructors
  and `IOptions<T>` consumers must adopt the responsible option type and be recompiled; no database migration.
- **Breaking notifier contract.** `INotificationNotifier` is the sole channel execution contract: `Name` plus
  cancellation-aware single-recipient `DeliverAsync`. Delivery is best-effort — `DeliverAsync` returns `Task`
  (no result type); a notifier skips a recipient by returning, and a throw is logged and dropped by the Core
  handler, not retried. The Core-owned distributed-event handler adapts transport, so channel plugins do not
  implement an event-handler interface.
- **Breaking distributed-event contract.** The default distributor publishes single-recipient/channel
  `NotificationDeliveryRequestedEto` (wire name `Dignite.Abp.Notifications.NotificationDeliveryRequested`) instead of
  the legacy batched aggregate event. Quiesce publication, drain old aggregate events, upgrade consumers, then producers.
- **Breaking distributed-event payload envelope.** `NotificationDeliveryRequestedEto.Data` (a live, abstract
  `NotificationData`) is replaced by `DataJson`: the payload pre-serialized as discriminator-tagged JSON via
  `INotificationDataSerializer` at the distributor publish boundary and hydrated at the notifier boundary
  (`NotificationPayload.FromRequest(request, dataSerializer)`; both built-in notifiers inject the serializer). ABP's
  event bus — including the transactional outbox/inbox — serializes ETOs with plain System.Text.Json and no
  app-level options, so an abstract member cannot round-trip the box; a pre-serialized string survives any transport
  and stays readable for non-.NET consumers. Recompile channel plugins and rebuild custom notifiers against the new
  member. The rename is deliberate: pre-upgrade outbox rows (whose `Data` was written lossily) drain as a null
  payload under the new contract instead of poisoning the sender with a type mismatch.
- **Breaking pre-stable naming cleanup.** The single-recipient payload type `NotificationDelivery` (added in
  10.0.0-preview.2) is renamed to `NotificationPayload`, and its factory `FromWorkItem` to `FromRequest`, so the
  "delivery" vocabulary names only the act while the payload type reads as content. The delivery event's wire name is
  normalized from `Dignite.Abp.Notifications.NotificationDeliveryWork` to
  `Dignite.Abp.Notifications.NotificationDeliveryRequested` to match its `NotificationDeliveryRequestedEto` contract
  name. Recompile channel plugins/remote consumers and drain any in-flight events on the old wire name before upgrading.
- **Breaking for MongoDB context implementers.** `INotificationCenterMongoDbContext` extends ABP's `IHasEventInbox`
  and `IHasEventOutbox`. Consumer-owned implementations must expose and model the two ABP event collections and
  configure both boxes against their custom context. Notification business records require no backfill or rename.
- **Breaking behavior for callers.** An explicit `userIds` array no longer bypasses a notification definition's
  permission and feature requirements: `PublishAsync` filters explicit and subscription-derived recipients through
  the same `INotificationDefinitionManager.IsAvailableAsync` check, in the notification's tenant or host context.
- **Breaking behavior for direct distributor callers.** `NotificationInfo.TenantId` is authoritative for
  subscription lookup, eligibility, persistence, and event/outbox publication. `null` always means host and never
  falls back to an ambient tenant, so tenant-side callers of `INotificationDistributor` must set it explicitly.
- Subscription-driven distribution treats a definition-wide subscription as a fallback for every entity and combines
  it with an exact entity subscription without delivering twice to the same user. Subscription uniqueness uses
  non-null, ordinal identity keys across EF Core and MongoDB; existing databases need a host-owned backfill and
  index migration as documented in the README.

### Fixed

- Hosts routing the distributed event bus through ABP's transactional outbox (`UseNotificationCenterEfCoreOutbox()`
  / `UseNotificationCenterMongoDbOutbox()`) could never deliver to SignalR or email: draining
  `NotificationDeliveryRequestedEto` from the box threw `System.NotSupportedException` on the abstract `Data`
  member (ABP deserializes outbox/inbox payloads with plain System.Text.Json), and the write side had already
  dropped the derived payload fields. Fixed by the `DataJson` envelope above; the shared outbox contract tests now
  drain both event boxes and assert the concrete payload survives. ([#118](https://github.com/dignite-projects/abp-notifications/issues/118))
- Notification definition names and `NotificationData` discriminators use explicit ordinal, case-sensitive
  registration and lookup. Conflicting registrations fail during application startup with both providers or CLR
  mappings identified instead of silently replacing an earlier value. Definition providers are discovered across
  module assemblies and duplicate registrations of the same provider execute once.
- Consistent explicit-recipient semantics across inline and background distribution: `null` resolves subscriptions,
  an empty list is a no-op, and duplicate explicit user IDs are normalized before threshold selection, persistence,
  and channel delivery.
- `NotificationDistributionJob` had two public `ExecuteAsync` overloads, so ABP's `BackgroundJobExecuter` (which
  resolves a job's execute method by name via reflection) threw `AmbiguousMatchException` for every
  background-dispatched distribution — any publish without explicit `userIds` (subscription-driven notifications,
  or a large explicit fan-out) silently failed while the triggering AppService call still returned 200/204. The
  second overload is renamed to `ExecuteWithCancellationAsync`. Fixing that exposed the job never opening a Unit of
  Work, so `NotificationStore.GetSubscriptionUserIdsAsync` threw `ObjectDisposedException` on the `DbContext`; the
  job now wraps distribution in `UnitOfWorkManager.Begin(requiresNew: true)` so the notification insert, inbox
  rows, and outbox event write commit atomically.

## [10.0.0-rc.2] - 2026-07-16

### Changed

- Renamed the Angular package to `@dignite/ng.notification-center`, making the UI framework
  explicit and leaving room for parallel React or other client packages.
- Clarified that npm requires every package to have a `latest` dist-tag, so a package whose first
  public version is a pre-release temporarily exposes that version as both `next` and `latest`.

## [10.0.0-rc.1] - 2026-07-16

### Added

- Added CI coverage for the Angular library and production demo build.
- Added isolated consumption smoke tests for all 15 packed NuGet packages and both Angular package
  entry points.

### Changed

- Synchronized the Angular package version with the NuGet release version.
- Renamed the Angular package to `@dignite/abp-notification-center` so all Dignite npm packages
  share the `@dignite` organization scope.
- Tagged pre-releases are now published publicly to NuGet.org and npm in addition to the existing
  GitHub Packages preview feed.
- Replaced the long-lived NuGet API key with NuGet.org Trusted Publishing and a short-lived OIDC
  credential issued to the tagged release workflow.
- Expanded the README with package installation commands, a compatibility table, and migration
  guidance for legacy 3.x consumers.

## [10.0.0-preview.2] - 2026-07-10

> `MAJOR` tracks the targeted ABP Framework version, so a breaking change to this module's own
> contracts arrives in a `MINOR` bump. Entries below marked **Breaking** require action when
> upgrading.

### Added

- Added `Dignite.Abp.Notifications.Emailing.Identity`, an optional ABP Identity-backed
  `IEmailNotificationAddressResolver` for the Emailing notifier.
- `NotificationDeliveryRequestedEto` and `NotificationDelivery` now carry the notification's `EntityTypeName` and
  `EntityId`, so a notifier can identify the business entity a notification is about without depending on
  Core.
- Added `NotificationEmailContentProvider<TData>`, a base class that narrows `NotificationData` once so an
  implementer cannot forget the type guard and accidentally claim every notification.
- Email address resolvers can now return an optional recipient culture; email content is built inside that culture
  and falls back to `NotificationEmailOptions.DefaultCulture`.

### Changed

- Changed email address resolution to use `EmailNotificationAddressResolveContext`, making
  `TenantId` explicit to local and remote resolver implementations.
- **Breaking.** `NotificationEntityIdentifier` now takes `(string entityTypeName, string entityId)` instead of
  `(Type entityType, object entityId)` — pass a short, stable name such as `"Demo.Order"`. Persisted
  `EntityTypeName` values are therefore no longer `Type.FullName`, so subscription rows and
  `NotificationCenterWebOptions.EntityLinkResolvers` keys written in the old format stop matching.
- **Breaking.** `IEmailNotificationAddressResolver` gained an `Order` member, and `GetEmailOrNullAsync` returns
  `Task<EmailNotificationAddress?>` rather than `Task<string?>`. Resolvers now form an ordered chain in which the
  first non-null address wins.
- **Breaking.** `IdentityEmailNotificationAddressResolver` no longer declares `[Dependency(ReplaceServices = true)]`.
  It joins the chain at `NotificationEmailProviderOrders.BuiltInFallback`, so an application resolver composes with
  it rather than displacing it.
- **Breaking.** Renamed `NotificationEmailContentProviderOrders` to `NotificationEmailProviderOrders`, which now
  orders both the content-provider and the address-resolver chains.
- **Breaking.** `NotificationEmailBuildContext`'s constructor gained a `cultureName` parameter.
- Documented that atomic persist-and-publish, and deduplication of a redelivered event, require the host to enable
  ABP's transactional outbox — `UseNotificationCenterEfCoreOutbox()` on EF Core. The MongoDB provider wires neither.
  No behaviour changed; the previous comments and README described a guarantee that was conditional.

### Fixed

- `EmailNotifier` no longer aborts the whole delivery when a recipient's culture name cannot be parsed. It falls
  back to `NotificationEmailOptions.DefaultCulture` and then to the ambient culture, logging a warning for each
  rejected value. Previously a single malformed per-user language setting threw out of the event handler, so every
  recipient after it received nothing and a redelivery re-mailed the ones before it. As part of this,
  `NotificationEmailBuildContext.CultureName` now accepts the empty string, which is the invariant culture.

### Removed

- **Breaking.** Removed `NullEmailNotificationAddressResolver` — an empty resolver chain already resolves no
  address.

## [10.0.0-preview.1] - 2026-07-09

### Added

- Initial public release: an event-driven, pluggable notification framework for ABP Framework
  (`core/`), plus an optional Notification Center providing a persistent inbox, subscriptions,
  read/unread state, and a REST API, with MVC and Angular UI libraries
  (`notification-center/`).
- Dual persistence support (EF Core and MongoDB) behind a shared `INotificationStore` abstraction.
- Real-time push (SignalR) and email notifiers.
- Optional permission gating for notification definitions via ABP Identity.
