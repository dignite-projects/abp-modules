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
 * An alias cannot help a package that imports ITSELF. Its primary entry point imports its secondary
 * ones (`@dignite/ng.x/config`, `@dignite/ng.x/proxy`), and a secondary entry point may import the
 * primary one, by the package's own name - and no package of that name is ever installed from
 * GitHub Packages, only `@dignite-projects/ng.x`. A bundler fails on the first such import ("Could
 * not resolve '@dignite/ng.x/config'"). So the import specifiers in the package's code (`.mjs`,
 * `.js`, `.d.ts`) that name the package itself are rewritten to the new name too, and each subpath
 * they name is checked against the manifest's `exports`.
 *
 * Source maps are left as they are: their `sourcesContent` is the original source text, which no
 * bundler resolves imports from, and only the columns of the rewritten import lines move.
 *
 * Usage: node build/rewrite-for-github-packages.mjs <unpacked package dir>
 * Prints the new package name on stdout, and nothing else; what it rewrote goes to stderr.
 */

import { readFileSync, readdirSync, writeFileSync } from 'node:fs';
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

const oldName = manifest.name;
const newName = toScope + oldName.slice(fromScope.length);
manifest.name = newName;

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

const rewritten = rewriteSelfReferences(packageDir, oldName, newName, manifest.exports);
console.error(`${newName}: ${rewritten} import specifier(s) of the package itself rewritten.`);
console.log(manifest.name);

/**
 * Rewrites the import specifiers in the package's code that name the package itself: `from '<old>'`,
 * `from '<old>/sub'`, `import('<old>/sub')` and `import '<old>'`, with either quote. Lines of a
 * comment are left alone: they are documentation, not an import. Every subpath that is rewritten
 * must be an entry of `exports`, or the consumer would still not resolve it. Returns how many
 * specifiers it rewrote.
 */
function rewriteSelfReferences(directory, oldName, newName, exports) {
  const escaped = oldName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

  // The name, then an optional subpath, as the whole of a quoted string that follows `from`, `import`
  // or `import(`. `@dignite/ng.flex-fields` does not match `@dignite/ng.flex-fields-ckeditor`: the
  // name is followed by a quote or a slash.
  const specifier = new RegExp(
    String.raw`(\bfrom\s*|\bimport\s*\(\s*|\bimport\s+)(["'])` + escaped + String.raw`((?:/[^"'\s]*)?)\2`,
    'g',
  );

  // Whatever still names the package inside a string after the rewrite is a reference this script did
  // not understand: fail, rather than publish a package that does not build.
  const leftover = new RegExp('["\'`]' + escaped + '["\'`/]');
  const isComment = (line) => /^\s*(\/\/|\/\*|\*)/.test(line);

  let count = 0;
  for (const file of listCodeFiles(directory)) {
    const lines = readFileSync(file, 'utf8').split('\n');
    let changed = false;

    for (let i = 0; i < lines.length; i++) {
      if (isComment(lines[i])) {
        continue;
      }

      lines[i] = lines[i].replace(specifier, (_, lead, quote, subpath) => {
        const key = subpath ? `.${subpath}` : '.';
        if (!exports || !Object.hasOwn(exports, key)) {
          throw new Error(
            `${file}: '${oldName}${subpath}' is imported by the package itself, but its package.json 'exports' has no '${key}'.`,
          );
        }

        count++;
        changed = true;
        return `${lead}${quote}${newName}${subpath}${quote}`;
      });

      if (leftover.test(lines[i])) {
        throw new Error(`${file}:${i + 1}: still names '${oldName}' after the rewrite: ${lines[i].trim()}`);
      }
    }

    if (changed) {
      writeFileSync(file, lines.join('\n'));
    }
  }

  return count;
}

function listCodeFiles(directory) {
  const files = [];
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) {
      if (entry.name !== 'node_modules') {
        files.push(...listCodeFiles(path));
      }
    } else if (/\.(mjs|cjs|js|d\.ts|d\.mts)$/.test(entry.name)) {
      files.push(path);
    }
  }

  return files;
}
