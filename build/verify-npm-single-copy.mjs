/**
 * Installs every published `@dignite/*` Angular package from npmjs with **Yarn Classic** and asserts
 * that each one resolves to exactly one copy, at the expected version.
 *
 * This exists because of the failure mode issue #211 describes, which nothing else in this
 * repository could see:
 *
 * - The five Angular packages ship in lockstep, and two of them depend on their siblings. If such a
 *   range is wider than the release version (`^10.0.0-rc.4` long after everything ships
 *   `10.0.0-rc.13`), a resolver may satisfy it with an older sibling rather than deduplicating
 *   against the copy already at the root.
 * - That is not a wasted-bytes problem. Angular DI keys off object identity and `FLEX_FIELD_TYPES`
 *   is a module-scoped `InjectionToken`, so two copies of `@dignite/ng.flex-fields` are two distinct
 *   DI keys: `provideCKEditorFieldType()` registers into one while `FieldTypeResolver` reads the
 *   other, and every field type looks unregistered at runtime. Nothing fails at install or at build.
 * - `verify-version-lockstep.ps1` now rejects a drifted range before a release goes out, which stops
 *   this at the source. This script is the other half: it checks the *resolved* outcome, so a future
 *   duplicate arriving by some other route — a transitive `@dignite/*` edge, a dist-tag that makes a
 *   range resolve backwards — is caught too.
 *
 * Yarn Classic specifically, because that is what all three Angular workspaces here and every
 * downstream consumer use, while the packed-tarball smoke tests install with npm. npm deduplicates
 * where Yarn Classic does not, so the verification path and the real usage path used to differ in
 * exactly the way that hid this defect.
 *
 * Usage: node build/verify-npm-single-copy.mjs <expected-version>
 */

import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const [, , expectedVersion] = process.argv;

if (!expectedVersion) {
  throw new Error('Usage: node verify-npm-single-copy.mjs <expected-version>');
}

/** Every package this repository publishes to npmjs, including the two that depend on siblings. */
const packages = [
  '@dignite/ng.file-explorer',
  '@dignite/ng.notification-center',
  '@dignite/ng.flex-fields',
  '@dignite/ng.flex-fields-file-explorer',
  '@dignite/ng.flex-fields-ckeditor',
];

const tempRoot = mkdtempSync(join(tmpdir(), 'dignite-npm-single-copy-'));

const run = (command, args) => {
  const executable = process.platform === 'win32' ? (process.env.ComSpec ?? 'cmd.exe') : command;
  const commandArguments =
    process.platform === 'win32' ? ['/d', '/s', '/c', command, ...args] : args;

  execFileSync(executable, commandArguments, { cwd: tempRoot, stdio: 'inherit', env: process.env });
};

/**
 * Every `@dignite/*` package anywhere in the tree, including nested `node_modules`, which is where a
 * duplicate would sit. Keyed by package name so one name resolving to several directories - the
 * whole point of this check - is visible rather than collapsed.
 */
const collectInstalled = (directory, found = new Map()) => {
  let entries;
  try {
    entries = readdirSync(directory, { withFileTypes: true });
  } catch {
    return found;
  }

  for (const entry of entries) {
    if (!entry.isDirectory()) {
      continue;
    }

    const path = join(directory, entry.name);

    if (entry.name === 'node_modules') {
      const scope = join(path, '@dignite');
      let scoped;
      try {
        scoped = readdirSync(scope, { withFileTypes: true });
      } catch {
        scoped = [];
      }

      for (const packageDirectory of scoped) {
        if (!packageDirectory.isDirectory()) {
          continue;
        }

        const packagePath = join(scope, packageDirectory.name);
        const manifest = JSON.parse(readFileSync(join(packagePath, 'package.json'), 'utf8'));
        const copies = found.get(manifest.name) ?? [];
        copies.push({ path: packagePath, version: manifest.version });
        found.set(manifest.name, copies);
      }
    }

    collectInstalled(path, found);
  }

  return found;
};

try {
  writeFileSync(
    join(tempRoot, 'package.json'),
    `${JSON.stringify(
      {
        name: 'dignite-npm-single-copy-check',
        private: true,
        // Caret ranges, not exact pins: a consumer writes `^`, and it is the resolution of a caret
        // that a stale sibling range or a backwards dist-tag can send to an older version.
        dependencies: Object.fromEntries(packages.map(name => [name, `^${expectedVersion}`])),
      },
      null,
      2,
    )}\n`,
  );

  // --ignore-scripts: nothing here is built or run, only resolved. --non-interactive so a prompt
  // cannot hang the release job. Retried because npmjs needs a moment to serve a version that this
  // same workflow run has only just published.
  const attempts = 5;
  for (let attempt = 1; ; attempt++) {
    try {
      run('npx', ['--yes', 'yarn@1', 'install', '--ignore-scripts', '--non-interactive', '--no-progress']);
      break;
    } catch (error) {
      if (attempt === attempts) {
        throw error;
      }
      const delaySeconds = attempt * 10;
      console.log(
        `yarn install failed (attempt ${attempt}/${attempts}); retrying in ${delaySeconds}s in case npmjs has not served the new version yet.`,
      );
      execFileSync(process.execPath, ['-e', `setTimeout(() => {}, ${delaySeconds * 1000})`]);
    }
  }

  const installed = collectInstalled(tempRoot);
  const problems = [];

  for (const name of packages) {
    const copies = installed.get(name) ?? [];

    if (copies.length === 0) {
      problems.push(`${name}: not installed at all.`);
      continue;
    }

    if (copies.length > 1) {
      const detail = copies.map(copy => `${copy.version} at ${copy.path}`).join(', ');
      problems.push(`${name}: installed ${copies.length} times (${detail}).`);
      continue;
    }

    if (copies[0].version !== expectedVersion) {
      problems.push(`${name}: resolved to ${copies[0].version}, expected ${expectedVersion}.`);
    }
  }

  // A @dignite package pulled in transitively that nobody listed is worth failing on too: it is
  // either a dependency that should have been declared, or the duplicate this check exists to find.
  for (const [name, copies] of installed) {
    if (packages.includes(name)) {
      continue;
    }
    problems.push(`${name}: unexpected @dignite package in the tree (${copies.length} copies).`);
  }

  if (problems.length > 0) {
    throw new Error(
      `Yarn Classic did not resolve a single copy of every @dignite package at ${expectedVersion}:\n  ${problems.join('\n  ')}\n` +
        'See https://github.com/dignite-projects/abp-modules/issues/211 for why a duplicate breaks field-type registration silently.',
    );
  }

  console.log(
    `Yarn Classic resolved exactly one copy of each of the ${packages.length} @dignite packages, all at ${expectedVersion}.`,
  );
} finally {
  rmSync(tempRoot, { recursive: true, force: true });
}
