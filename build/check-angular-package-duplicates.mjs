// Fails when a bare or scoped package is installed more than once under one workspace's
// `node_modules`.
//
// Angular libraries register through module-scoped `new InjectionToken(...)` values, so two copies
// of the same package are two distinct DI keys, not a wasted-bytes problem. `verify-npm-single-copy.mjs`
// already guards this for the five `@dignite/*` packages this repository publishes (issue #211:
// `FLEX_FIELD_TYPES` split across two copies of `@dignite/ng.flex-fields` made `provideCKEditorFieldType()`
// register into one copy while `FieldTypeResolver` read the other, so a field type looked unregistered
// at runtime with nothing failing at install or build time). This script is the same check for the
// other shape the bug takes: a bare, non-`@dignite`-scoped package that a demo app's own `package.json`
// happens to conflict with.
//
// `ng-zorro-antd` is the concrete case: `@abp/ng.components` (a real `dependency`, not a peer, of
// `@dignite/ng.flex-fields` and `@dignite/ng.file-explorer` as of `6f039ef`) pins it at
// `~21.0.0-next.1`, i.e. `<21.1.0`. A demo app declaring `^21.3.3` for its own use does not satisfy
// that range, so Yarn Classic nests a second copy under `@abp/ng.components/node_modules`. Two copies
// are two module-scoped `NZ_CONFIG`/`NzConfigService` tokens: `provideNzConfig()`/`provideNzI18n()`
// at the app root only reach the copy the app's own components resolve, not the one `@abp/ng.components`'
// controls (e.g. `abp-tree`) see. `@angular/cdk` reaches these same packages through the identical
// route - `@abp/ng.components` depends on `ng-zorro-antd`, which depends on `@angular/cdk` - so it
// carries the same risk even though no version conflict currently splits it.
//
// A target that matches nothing installed fails the run rather than passing vacuously: a typo'd
// target, or pointing this at the wrong node_modules, is otherwise indistinguishable from a clean
// tree, and this check is only as good as the list it is given.
//
// Deliberately not run against a fresh `npm install`: npm deduplicates the exact graph that Yarn
// Classic splits, so this only means something run against a tree installed the way this
// repository's workspaces and every downstream consumer actually install (Yarn Classic).
//
// Usage: node build/check-angular-package-duplicates.mjs <node_modules-dir> <target> [<target> ...]
//        e.g. node build/check-angular-package-duplicates.mjs flex-fields/angular/node_modules ng-zorro-antd @angular/cdk
//
// A target is either a scope (`@dignite` - matches every package under it) or an exact package name
// (`ng-zorro-antd`, or a fully-qualified `@angular/cdk`). There is no bare-word-means-scope guess: a
// leading `@` with no `/` is the one shape npm reserves for a scope, everything else is matched
// exactly, which is what makes an unscoped package name like `ng-zorro-antd` expressible at all.
// There is no default target list - unlike `verify-npm-single-copy.mjs`'s fixed five `@dignite/*`
// packages, what to check here varies per workspace (see ci.yml), so it must be named explicitly
// rather than silently checking nothing when a caller forgets an argument.

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';

const [nodeModulesArgument, ...targets] = process.argv.slice(2);

if (!nodeModulesArgument || targets.length === 0) {
  throw new Error(
    'Usage: node build/check-angular-package-duplicates.mjs <node_modules-dir> <target> [<target> ...]',
  );
}

const nodeModulesRoot = resolve(nodeModulesArgument);
if (!statSync(nodeModulesRoot, { throwIfNoEntry: false })?.isDirectory()) {
  throw new Error(`Not a directory: ${nodeModulesRoot} (run the install first)`);
}

const isScope = target => target.startsWith('@') && !target.includes('/');
const matchesTarget = (target, packageName) =>
  isScope(target) ? packageName.startsWith(`${target}/`) : packageName === target;

const readManifest = packageDirectory => {
  try {
    return JSON.parse(readFileSync(join(packageDirectory, 'package.json'), 'utf8'));
  } catch {
    return null; // A directory under node_modules that is not a package (.bin, .cache, ...).
  }
};

/**
 * Every installed copy of a matching package anywhere under `directory`, including nested
 * `node_modules` - which is exactly where a duplicate hides. Keyed by package name so one name
 * resolving to several directories, the whole point of this check, stays visible instead of being
 * collapsed by a `Map` overwrite.
 */
const collectInstalls = (directory, installs = new Map()) => {
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    if (!entry.isDirectory() || entry.name.startsWith('.')) continue;

    const packageDirectories = entry.name.startsWith('@')
      ? readdirSync(join(directory, entry.name), { withFileTypes: true })
          .filter(inner => inner.isDirectory())
          .map(inner => join(directory, entry.name, inner.name))
      : [join(directory, entry.name)];

    for (const packageDirectory of packageDirectories) {
      const manifest = readManifest(packageDirectory);
      if (manifest?.name && targets.some(target => matchesTarget(target, manifest.name))) {
        const copies = installs.get(manifest.name) ?? [];
        copies.push({
          version: manifest.version,
          path: relative(nodeModulesRoot, packageDirectory).replace(/\\/g, '/'),
        });
        installs.set(manifest.name, copies);
      }

      const nested = join(packageDirectory, 'node_modules');
      if (statSync(nested, { throwIfNoEntry: false })?.isDirectory()) {
        collectInstalls(nested, installs);
      }
    }
  }

  return installs;
};

const installs = collectInstalls(nodeModulesRoot);

// Checked per target rather than "did anything match at all" - with several targets in one
// invocation, one broad match would otherwise cover for a mistyped target next to it and silently
// drop that package out of the check.
const unmatchedTargets = targets.filter(
  target => ![...installs.keys()].some(name => matchesTarget(target, name)),
);

if (unmatchedTargets.length > 0) {
  console.error(
    `✗ Nothing matching ${unmatchedTargets.join(', ')} is installed under ${nodeModulesRoot}. ` +
      'Either the install did not run, or the target is wrong (a scope needs its leading "@" and no ' +
      'slash; anything else is matched as an exact package name) - failing rather than reporting a ' +
      'vacuous pass.',
  );
  process.exitCode = 1;
} else {
  const duplicated = [...installs].filter(([, copies]) => copies.length > 1);

  if (duplicated.length === 0) {
    console.log(
      `✓ ${installs.size} package(s) matching ${targets.join(', ')} are installed exactly once each.`,
    );
  } else {
    console.error('✗ These packages are installed more than once:');
    for (const [name, copies] of duplicated.sort(([a], [b]) => a.localeCompare(b))) {
      console.error(`    ${name}`);
      for (const copy of copies.sort((a, b) => a.path.localeCompare(b.path))) {
        console.error(`      ${copy.version.padEnd(16)} ${copy.path}`);
      }
    }
    console.error(
      '  Each copy carries its own module-scoped InjectionToken values, so a provider registered ' +
        'against one copy is invisible to a consumer resolving the other. Narrow whichever ' +
        "workspace's package.json declares a wider range than the other copy's source allows, the " +
        'way flex-fields/angular and file-storing/angular narrowed ng-zorro-antd to `~21.0.2` to ' +
        'match @abp/ng.components\' `<21.1.0` ceiling.',
    );
    process.exitCode = 1;
  }
}
