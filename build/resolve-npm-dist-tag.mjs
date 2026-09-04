/**
 * Prints the npm dist-tag a release should publish under: `latest` or `next`.
 *
 * A stable version always takes `latest`. A pre-release takes `latest` **only while no stable version
 * of these packages has ever been published**, and `next` from then on.
 *
 * The first half of that rule exists because `latest` is what a bare `npm install <pkg>` resolves,
 * and what a resolver prefers for any range that admits it. Publishing every pre-release under `next`
 * alone left `latest` pinned to whatever pre-release happened to be current when the convention
 * changed — `10.0.0-rc.11`, while `10.0.0-rc.13` was the newest published — so a bare install handed
 * out a version three releases behind, and Yarn Classic resolved intra-repo ranges backwards onto it.
 * That was the second of the three conditions behind issue #211.
 *
 * The second half is why this is a script rather than a one-line flip. Once a stable `10.0.0` exists,
 * `latest` must stay on it: a later `10.1.0-rc.1` taking `latest` would silently move every consumer
 * doing a bare install from a stable release onto a pre-release, which is worse than the problem this
 * solves. Reading the registry rather than hardcoding a date or a flag means the rule retires itself
 * the moment the first stable ships, with nobody having to remember to come back and change it.
 *
 * Usage: node build/resolve-npm-dist-tag.mjs <version>
 */

import { execFileSync } from 'node:child_process';

const [, , version] = process.argv;

if (!version) {
  throw new Error('Usage: node resolve-npm-dist-tag.mjs <version>');
}

/** Every package this repository publishes to npmjs. They are versioned in lockstep. */
const packages = [
  '@dignite/ng.file-explorer',
  '@dignite/ng.notification-center',
  '@dignite/ng.flex-fields',
  '@dignite/ng.flex-fields-file-explorer',
  '@dignite/ng.flex-fields-ckeditor',
];

const isPrerelease = candidate => candidate.includes('-');

if (!isPrerelease(version)) {
  console.log('latest');
  process.exit(0);
}

// Node refuses to spawn a .cmd directly on Windows (EINVAL), so route through the shell there -
// the same wrapper the Angular package smoke tests use.
const npmCommand = process.platform === 'win32' ? (process.env.ComSpec ?? 'cmd.exe') : 'npm';
const npmArguments = args => (process.platform === 'win32' ? ['/d', '/s', '/c', 'npm', ...args] : args);

/**
 * Published versions of one package, or an empty list if it has never been published. A registry
 * lookup needs no credentials, which matters here: npmjs publishing in this workflow is OIDC-based
 * and has no standing token to authenticate anything else with.
 */
const publishedVersions = name => {
  try {
    const output = execFileSync(npmCommand, npmArguments(['view', name, 'versions', '--json']), {
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    const parsed = JSON.parse(output);
    return Array.isArray(parsed) ? parsed : [parsed];
  } catch (error) {
    // A package that has never been published (E404) has no stable release by definition. Anything
    // else - a network failure, a registry outage - must not be read as "no stable version exists",
    // because that would hand `latest` to a pre-release on the strength of a failed lookup.
    const message = `${error.stdout ?? ''}${error.stderr ?? ''}${error.message ?? ''}`;
    if (message.includes('E404') || message.includes('404 Not Found')) {
      return [];
    }
    throw new Error(`Could not read published versions of ${name}: ${message}`);
  }
};

const stableReleases = packages.flatMap(name =>
  publishedVersions(name)
    .filter(candidate => !isPrerelease(candidate))
    .map(candidate => `${name}@${candidate}`),
);

if (stableReleases.length > 0) {
  console.error(
    `A stable release already exists (${stableReleases.slice(0, 3).join(', ')}${stableReleases.length > 3 ? ', …' : ''}), so this pre-release publishes under "next" and leaves "latest" on the stable line.`,
  );
  console.log('next');
} else {
  console.error(
    'No stable release of these packages has been published yet, so this pre-release also takes "latest" - otherwise a bare `npm install` would resolve to an older pre-release. See issue #211.',
  );
  console.log('latest');
}
