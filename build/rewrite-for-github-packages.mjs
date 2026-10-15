/**
 * Rewrites an unpacked Angular package for GitHub Packages, in place.
 *
 * GitHub Packages' npm registry only accepts a scope equal to the owning org, so `@dignite/ng.x`
 * is published there as `@dignite-projects/ng.x`. Renaming the package alone is not enough once
 * pre-releases live only on GitHub Packages: `@dignite/ng.flex-fields-ckeditor` depends on
 * `@dignite/ng.flex-fields` at the exact release version, and that version no longer exists on
 * npmjs, so the dependency would fail to resolve. Every `@dignite/*` dependency is therefore
 * rewritten to an npm alias of its GitHub Packages copy, keeping the name the code imports:
 *
 *   "@dignite/ng.flex-fields": "10.8.0-rc.1"
 *   -> "@dignite/ng.flex-fields": "npm:@dignite-projects/ng.flex-fields@10.8.0-rc.1"
 *
 * A consumer declares the same alias for the packages it uses directly (CONTRIBUTING.md ->
 * "Pre-releases: GitHub Packages only"), so its own entry and this one are one identical spec and
 * install as a single copy - the property issue #211 is about.
 *
 * Usage: node build/rewrite-for-github-packages.mjs <unpacked package dir>
 * Prints the new package name.
 */

import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const [, , packageDir] = process.argv;

if (!packageDir) {
  throw new Error('Usage: node rewrite-for-github-packages.mjs <unpacked package dir>');
}

const fromScope = '@dignite/';
const toScope = '@dignite-projects/';
const manifestPath = join(packageDir, 'package.json');
const manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));

if (!manifest.name.startsWith(fromScope)) {
  throw new Error(`Expected a ${fromScope}* package, got '${manifest.name}'.`);
}

manifest.name = toScope + manifest.name.slice(fromScope.length);

for (const section of ['dependencies', 'peerDependencies', 'optionalDependencies']) {
  const dependencies = manifest[section];
  if (!dependencies) {
    continue;
  }

  for (const [name, range] of Object.entries(dependencies)) {
    if (name.startsWith(fromScope)) {
      dependencies[name] = `npm:${toScope}${name.slice(fromScope.length)}@${range}`;
    }
  }
}

writeFileSync(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`);
console.log(manifest.name);
