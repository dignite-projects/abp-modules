// Fails when a built Angular library imports a package its own package.json does not declare.
//
// ng-packagr marks every bare specifier it does not bundle as an external, but it never checks that
// the emitted externals are actually declared. A library can therefore publish a bundle that says
// `from '@ngx-validate/core'` while its package.json mentions no such dependency: nothing fails at
// build time, nothing fails at `npm install`, and the consumer gets an unresolvable specifier the
// first time they build their own app. All five libraries in this repository had at least one.
//
// The per-module `smoke-test-angular-package.mjs` cannot catch this: it seeds the throwaway consumer
// with `...workspacePackage.dependencies`, i.e. the demo app's hand-maintained dependency list, so
// every undeclared package is already installed before the compile it verifies. It answers "does the
// public API still compile", which is a different and equally worthwhile question.
//
// Usage: node build/check-angular-package-deps.mjs <dist-dir> [<dist-dir> ...]
//        e.g. node build/check-angular-package-deps.mjs flex-fields/angular/dist/flex-fields

import { builtinModules } from 'node:module';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, resolve } from 'node:path';

const distDirectories = process.argv.slice(2);

if (distDirectories.length === 0) {
  throw new Error('Usage: node build/check-angular-package-deps.mjs <dist-dir> [<dist-dir> ...]');
}

const NODE_BUILTINS = new Set(builtinModules);

// Module specifiers only ever appear as string literals in these four positions. Block comments are
// stripped first: ng-packagr copies JSDoc into the emitted .mjs verbatim, and an example line like
// ` * import { pinyin } from 'pinyin-pro';` is otherwise indistinguishable from a real import.
// A `/*` inside a string literal could truncate the stripped text, which can only hide a specifier
// or surface a spurious one for a human to look at — never silently pass a real miss as declared.
const SPECIFIER_PATTERNS = [
  /(?:^|[\s;}])(?:import|export)\s[^;]*?\sfrom\s*['"]([^'"]+)['"]/g,
  /(?:^|[\s;}])import\s*['"]([^'"]+)['"]/g,
  /\bimport\s*\(\s*['"]([^'"]+)['"]\s*\)/g,
  /\brequire\s*\(\s*['"]([^'"]+)['"]\s*\)/g,
];

const stripComments = source =>
  source
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/^[ \t]*\/\/.*$/gm, '');

const toPackageName = specifier =>
  specifier.startsWith('@')
    ? specifier.split('/').slice(0, 2).join('/')
    : specifier.split('/')[0];

const collectSourceFiles = directory => {
  const found = [];
  for (const entry of readdirSync(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) {
      found.push(...collectSourceFiles(path));
    } else if (/\.(mjs|js|d\.ts)$/.test(entry.name)) {
      found.push(path);
    }
  }
  return found;
};

const checkPackage = distDirectory => {
  const root = resolve(distDirectory);
  if (!statSync(root, { throwIfNoEntry: false })?.isDirectory()) {
    throw new Error(`Not a built package directory: ${root}`);
  }

  const manifest = JSON.parse(readFileSync(join(root, 'package.json'), 'utf8'));
  const declared = new Set([
    ...Object.keys(manifest.dependencies ?? {}),
    ...Object.keys(manifest.peerDependencies ?? {}),
    ...Object.keys(manifest.optionalDependencies ?? {}),
  ]);

  const undeclared = new Map();

  for (const file of collectSourceFiles(root)) {
    const source = stripComments(readFileSync(file, 'utf8'));
    for (const pattern of SPECIFIER_PATTERNS) {
      pattern.lastIndex = 0;
      for (const match of source.matchAll(pattern)) {
        const specifier = match[1];
        if (specifier.startsWith('.') || specifier.startsWith('node:')) continue;

        const name = toPackageName(specifier);
        // A secondary entry point importing its own primary one is not an external dependency.
        if (name === manifest.name || NODE_BUILTINS.has(name) || declared.has(name)) continue;

        if (!undeclared.has(name)) undeclared.set(name, new Set());
        undeclared.get(name).add(file.slice(root.length + 1).replace(/\\/g, '/'));
      }
    }
  }

  if (undeclared.size === 0) {
    console.log(`✓ ${manifest.name}: every emitted specifier is declared.`);
    return true;
  }

  console.error(`✗ ${manifest.name}: imports packages it does not declare:`);
  for (const [name, files] of [...undeclared].sort(([a], [b]) => a.localeCompare(b))) {
    console.error(`    ${name}  (${[...files].sort().join(', ')})`);
  }
  console.error(
    '  Add each to "dependencies" if a consumer would not otherwise have it, or to ' +
      '"peerDependencies" if it is guaranteed by the ABP/Angular host. A new "dependencies" entry ' +
      'must also be listed in the library\'s ng-package.json "allowedNonPeerDependencies".',
  );
  return false;
};

const allClean = distDirectories.map(checkPackage).every(Boolean);
if (!allClean) process.exitCode = 1;
