/**
 * Installs every `@dignite/*` Angular package with **Yarn Classic** and asserts that each one
 * resolves to exactly one copy, at the expected version. Two modes share that check:
 *
 * - `packed` installs from the tarballs `npm pack` already produced locally, by pointing each
 *   dependency at its tarball with a `file:` path. This is the real pre-publish gate: it runs before
 *   either registry has been touched, so a failure here stops the release before anything is live.
 * - `published` installs the versions actually live on npmjs, after publishing. It stays for what
 *   `packed` cannot see from a local tarball: a dist-tag pointing at the wrong version, or any other
 *   resolution difference that only exists against the real registry. See release.yml's own comment
 *   on that step for why a failure this late is still worth having.
 *
 * Both modes exist because of the failure mode issue #211 describes, which nothing else in this
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
 * exactly the way that hid this defect in the first place. `packed` mode still runs that same Yarn
 * Classic resolution, just against the tarballs themselves rather than the registry: Yarn Classic
 * resolves a `file:` dependency by reading the tarball's own `package.json.version` and deciding
 * semver satisfaction and hoisting against every other edge in the graph exactly as it would for a
 * registry-resolved copy, so it is a faithful stand-in for what happens once these are actually
 * published — without needing anything published yet.
 *
 * Usage:
 *   node build/verify-npm-single-copy.mjs packed [<tarballs-root-directory>]
 *     Scans <tarballs-root-directory> (default: artifacts/npm) for one `*.tgz` per immediate
 *     subdirectory — the layout release.yml's pack steps already produce — and installs each by its
 *     absolute `file:` path. No expected version is needed: each tarball's own package.json is
 *     ground truth for what "correct" means here.
 *   node build/verify-npm-single-copy.mjs published <expected-version>
 *     Waits for the registry to actually serve <expected-version> of every package - a publish this
 *     same workflow run just made is accepted well before it is served - then installs them all and
 *     checks the resolved tree.
 */

import { execFileSync } from 'node:child_process';
import { gunzipSync } from 'node:zlib';
import { mkdtempSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve, sep } from 'node:path';

const [, , mode, ...rest] = process.argv;

const usageError = () => {
  throw new Error(
    'Usage:\n' +
      '  node build/verify-npm-single-copy.mjs packed [<tarballs-root-directory>]\n' +
      '  node build/verify-npm-single-copy.mjs published <expected-version>',
  );
};

if (mode !== 'packed' && mode !== 'published') {
  usageError();
}

/** Every package this repository publishes, including the two that depend on siblings. */
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

/** An absolute path, forward-slashed even on Windows — what Yarn Classic's `file:` protocol expects. */
const toFileDependency = path => `file:${resolve(path).split(sep).join('/')}`;

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

/**
 * Compares the installed tree against what each package's expected version should be (a fixed
 * version in `published` mode, or each tarball's own version in `packed` mode) and flags anything
 * that isn't exactly one copy at that version - including a `@dignite/*` package nobody listed,
 * which is either a dependency that should have been declared, or the duplicate this exists to find.
 */
const findProblems = (installed, expectedVersionOf) => {
  const problems = [];

  for (const name of packages) {
    const copies = installed.get(name) ?? [];
    const expectedVersion = expectedVersionOf(name);

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

  for (const [name, copies] of installed) {
    if (packages.includes(name)) {
      continue;
    }
    problems.push(`${name}: unexpected @dignite package in the tree (${copies.length} copies).`);
  }

  return problems;
};

const reportOrThrow = (problems, description) => {
  if (problems.length > 0) {
    throw new Error(
      `Yarn Classic did not resolve a single copy of every @dignite package (${description}):\n  ${problems.join('\n  ')}\n` +
        'See https://github.com/dignite-projects/abp-modules/issues/211 for why a duplicate breaks field-type registration silently.',
    );
  }

  console.log(
    `Yarn Classic resolved exactly one copy of each of the ${packages.length} @dignite packages (${description}).`,
  );
};

/** Reads `package/package.json` straight out of an npm-pack tarball, without shelling out to `tar`
 * (Git Bash's MSYS `tar` misparses a Windows drive letter like `D:\...` as a remote-host spec, and
 * relying on whatever `tar` happens to be first on PATH is exactly the kind of environment-dependent
 * behaviour this check exists to avoid). npm-pack tarballs are plain gzipped ustar archives with the
 * manifest as their first, short-named entry, so a minimal single-entry reader is enough. */
const readTarballManifest = tarballPath => {
  const tarBuffer = gunzipSync(readFileSync(tarballPath));
  let offset = 0;

  while (offset + 512 <= tarBuffer.length) {
    const header = tarBuffer.subarray(offset, offset + 512);
    if (header.every(byte => byte === 0)) {
      break; // end-of-archive marker
    }

    const name = header.subarray(0, 100).toString('utf8').replace(/\0.*$/, '');
    const size =
      parseInt(header.subarray(124, 136).toString('utf8').replace(/\0.*$/, '').trim(), 8) || 0;
    const typeFlag = String.fromCharCode(header[156]);
    const dataOffset = offset + 512;

    if (name === 'package/package.json' && (typeFlag === '0' || typeFlag === '\0')) {
      return JSON.parse(tarBuffer.subarray(dataOffset, dataOffset + size).toString('utf8'));
    }

    offset = dataOffset + Math.ceil(size / 512) * 512;
  }

  throw new Error(`package/package.json not found inside ${tarballPath} - is this an npm pack tarball?`);
};

/** One `*.tgz` per immediate subdirectory of `rootDirectory`, keyed by the package name each tarball's
 * own manifest declares - the layout release.yml's pack steps produce under `artifacts/npm/`. */
const findPackedTarballs = rootDirectory => {
  let subdirectories;
  try {
    subdirectories = readdirSync(rootDirectory, { withFileTypes: true }).filter(entry =>
      entry.isDirectory(),
    );
  } catch (error) {
    throw new Error(`Cannot read tarballs directory ${rootDirectory}: ${error.message}`);
  }

  const byPackage = new Map();

  for (const subdirectory of subdirectories) {
    const subdirectoryPath = join(rootDirectory, subdirectory.name);
    const tarballNames = readdirSync(subdirectoryPath).filter(file => file.endsWith('.tgz'));

    if (tarballNames.length !== 1) {
      throw new Error(`Expected exactly one .tgz in ${subdirectoryPath}, found ${tarballNames.length}.`);
    }

    const tarballPath = resolve(join(subdirectoryPath, tarballNames[0]));
    const manifest = readTarballManifest(tarballPath);
    byPackage.set(manifest.name, { version: manifest.version, path: tarballPath });
  }

  return byPackage;
};

const sleep = milliseconds => new Promise(resolve => setTimeout(resolve, milliseconds));

/** Same win32 `cmd /c` wrapping as `run`, but capturing stdout instead of inheriting it. */
const capture = (command, args) => {
  const executable = process.platform === 'win32' ? (process.env.ComSpec ?? 'cmd.exe') : command;
  const commandArguments =
    process.platform === 'win32' ? ['/d', '/s', '/c', command, ...args] : args;

  return execFileSync(executable, commandArguments, {
    cwd: tempRoot,
    encoding: 'utf8',
    env: process.env,
    stdio: ['ignore', 'pipe', 'pipe'],
  }).trim();
};

/** Whatever registry yarn is going to resolve against, rather than a hard-coded npmjs URL - the
 * release step that runs this points npm at an empty userconfig, so the two agree either way. */
const registryBaseUrl = () => {
  let configured = '';
  try {
    configured = capture('npm', ['config', 'get', 'registry']);
  } catch {
    configured = '';
  }

  const url = configured && configured !== 'undefined' ? configured : 'https://registry.npmjs.org/';
  return url.endsWith('/') ? url : `${url}/`;
};

/** Asks the registry directly whether a version is in a package's packument yet. A 404 means the
 * package - or, for a first-ever publish, anything under that name - is not being served yet, which
 * is a "not ready", not an error. */
const registryServesVersion = async (registry, name, version) => {
  const response = await fetch(`${registry}${name.replace('/', '%2f')}`, {
    // The abbreviated packument: the same `versions` map a resolver reads, a fraction of the bytes.
    headers: { accept: 'application/vnd.npm.install-v1+json', 'cache-control': 'no-cache' },
    signal: AbortSignal.timeout(15_000),
  });

  if (response.status === 404) {
    return false;
  }

  if (!response.ok) {
    throw new Error(`registry returned ${response.status} ${response.statusText}`);
  }

  const packument = await response.json();
  return Boolean(packument.versions?.[version]);
};

/**
 * Blocks until the registry actually serves `expectedVersion` of every package.
 *
 * npmjs records a publish asynchronously: `npm publish` returns as soon as the tarball is accepted -
 * printing "Your package is being processed and may take a few minutes to become available" - and the
 * version only enters the packument a resolver reads some time after that. On `v10.0.0-rc.16` the gap
 * was 126 seconds for `@dignite/ng.flex-fields` alone: `npm publish` printed
 * `+ @dignite/ng.flex-fields@10.0.0-rc.16` at 00:23:04Z, npmjs stamped the version 00:25:10Z, and the
 * five `yarn install` retries this used to rely on gave up at 00:24:59Z - twelve seconds early. That
 * failed the release for a race rather than for a duplicate, after every package was already public,
 * and left the GitHub Release uncreated because it is a later step in the same job.
 *
 * Retrying `yarn install` is the wrong instrument for this: yarn aborts on the first name it cannot
 * resolve, so its exit code cannot distinguish "one of five is still propagating" from "the published
 * set is broken", and each attempt burns a full resolution pass before sleeping. Waiting on the
 * registry itself makes that distinction structural - past this point, a `yarn install` failure is
 * about resolution, which is the only thing this check is qualified to judge.
 */
const waitForPublishedVersions = async expectedVersion => {
  const registry = registryBaseUrl();
  const timeoutSeconds = Number(process.env.DIGNITE_NPM_PROPAGATION_TIMEOUT_SECONDS ?? 900);
  const pollSeconds = 10;
  const deadline = Date.now() + timeoutSeconds * 1000;
  const pending = new Set(packages);

  console.log(
    `Waiting up to ${timeoutSeconds}s for ${registry} to serve ${expectedVersion} of all ${packages.length} @dignite packages.`,
  );

  for (;;) {
    for (const name of [...pending]) {
      try {
        if (await registryServesVersion(registry, name, expectedVersion)) {
          pending.delete(name);
          console.log(`  serving ${name}@${expectedVersion}`);
        }
      } catch (error) {
        console.log(`  ${name}: registry lookup failed (${error.message}); will ask again.`);
      }
    }

    if (pending.size === 0) {
      console.log(`All ${packages.length} packages are being served at ${expectedVersion}.`);
      return;
    }

    if (Date.now() >= deadline) {
      throw new Error(
        `${registry} still does not serve ${expectedVersion} of: ${[...pending].sort().join(', ')} - ` +
          `${timeoutSeconds}s after this run published them.\n` +
          'This is registry propagation, not the duplicate this check looks for, and nothing needs ' +
          're-publishing: re-run this step once npmjs has caught up, and raise ' +
          'DIGNITE_NPM_PROPAGATION_TIMEOUT_SECONDS if it keeps timing out.',
      );
    }

    await sleep(pollSeconds * 1000);
  }
};

const runPackedMode = rootDirectory => {
  const tarballs = findPackedTarballs(rootDirectory);

  const missing = packages.filter(name => !tarballs.has(name));
  if (missing.length > 0) {
    throw new Error(`No packed tarball found under ${rootDirectory} for: ${missing.join(', ')}.`);
  }

  const unexpected = [...tarballs.keys()].filter(name => !packages.includes(name));
  if (unexpected.length > 0) {
    throw new Error(`Found a packed tarball for unexpected package(s): ${unexpected.join(', ')}.`);
  }

  // `resolutions`, not just `dependencies`: a package that depends on a sibling (e.g.
  // flex-fields-file-explorer on flex-fields) declares that edge as a plain semver range inside its
  // own packed package.json, not as a `file:` path - it doesn't know at pack time that this check will
  // ever run. Yarn Classic resolves a `file:` request and a semver-range request for the same package
  // name as two independent lookups rather than reusing one to satisfy the other, so without an
  // override it goes to the npm registry for the range - and fails outright for a version that has
  // never been published there, which is exactly the case this check exists to run before. The
  // `resolutions` override forces every occurrence of a name in the tree onto the same local tarball
  // regardless of what range asked for it.
  const fileDependencies = Object.fromEntries(
    packages.map(name => [name, toFileDependency(tarballs.get(name).path)]),
  );

  writeFileSync(
    join(tempRoot, 'package.json'),
    `${JSON.stringify(
      {
        name: 'dignite-npm-single-copy-check',
        private: true,
        dependencies: fileDependencies,
        resolutions: fileDependencies,
      },
      null,
      2,
    )}\n`,
  );

  // No retry loop here, unlike published mode: these are local files already on disk, so there is no
  // registry propagation delay to wait out.
  run('npx', ['--yes', 'yarn@1', 'install', '--ignore-scripts', '--non-interactive', '--no-progress']);

  const installed = collectInstalled(tempRoot);
  const problems = findProblems(installed, name => tarballs.get(name).version);
  reportOrThrow(problems, 'each at the version its own packed tarball declares');
};

const runPublishedMode = async expectedVersion => {
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

  await waitForPublishedVersions(expectedVersion);

  // --ignore-scripts: nothing here is built or run, only resolved. --non-interactive so a prompt
  // cannot hang the release job. The handful of retries left is for a genuine transient - a CDN edge
  // lagging the packument the wait above just read, a dropped connection - and no longer for
  // propagation, which is now waited out explicitly against the registry. A real duplicate fails
  // identically on every attempt, so retrying this few times cannot hide one.
  const attempts = 3;
  for (let attempt = 1; ; attempt++) {
    try {
      run('npx', ['--yes', 'yarn@1', 'install', '--ignore-scripts', '--non-interactive', '--no-progress']);
      break;
    } catch (error) {
      if (attempt === attempts) {
        throw error;
      }
      const delaySeconds = attempt * 10;
      console.log(`yarn install failed (attempt ${attempt}/${attempts}); retrying in ${delaySeconds}s.`);
      await sleep(delaySeconds * 1000);
    }
  }

  const installed = collectInstalled(tempRoot);
  const problems = findProblems(installed, () => expectedVersion);
  reportOrThrow(problems, `all at ${expectedVersion}`);
};

try {
  if (mode === 'packed') {
    runPackedMode(rest[0] ?? 'artifacts/npm');
  } else {
    const expectedVersion = rest[0];
    if (!expectedVersion) {
      usageError();
    }
    await runPublishedMode(expectedVersion);
  }
} finally {
  rmSync(tempRoot, { recursive: true, force: true });
}
