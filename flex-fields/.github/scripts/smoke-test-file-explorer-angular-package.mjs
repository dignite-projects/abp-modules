import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const [, , tarballArgument, expectedVersion] = process.argv;

if (!tarballArgument || !expectedVersion) {
  throw new Error('Usage: node smoke-test-file-explorer-angular-package.mjs <package.tgz> <expected-version>');
}

const tarballPath = resolve(tarballArgument);
if (!existsSync(tarballPath)) {
  throw new Error(`Angular package tarball does not exist: ${tarballPath}`);
}

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, '..', '..');
const angularRoot = join(repositoryRoot, 'angular');
const workspacePackage = JSON.parse(
  readFileSync(join(angularRoot, 'package.json'), 'utf8'),
);

// The two peer libraries this package is built against. Both must already be built by the time
// this script runs (the core library earlier in the same flex-fields Angular section, file-storing's
// library in the section that runs before it) - resolved here by absolute path rather than trusting
// workspacePackage.dependencies, whose own `file:` entries are relative to flex-fields/angular and
// would resolve to nowhere from this script's temp directory under the OS temp root.
const flexFieldsLibDist = join(angularRoot, 'dist', 'flex-fields');
const fileExplorerLibDist = resolve(repositoryRoot, '..', 'file-storing', 'angular', 'dist', 'file-explorer');
for (const [name, path] of [['@dignite/ng.flex-fields', flexFieldsLibDist], ['@dignite/ng.file-explorer', fileExplorerLibDist]]) {
  if (!existsSync(path)) {
    throw new Error(`${name}'s build output does not exist yet: ${path}`);
  }
}

const tempRoot = mkdtempSync(join(tmpdir(), 'dignite-flex-fields-file-explorer-angular-smoke-'));
const npmCommand = process.platform === 'win32' ? 'npm.cmd' : 'npm';
const npxCommand = process.platform === 'win32' ? 'npx.cmd' : 'npx';

const run = (command, args) => {
  const executable = process.platform === 'win32' ? (process.env.ComSpec ?? 'cmd.exe') : command;
  const commandArguments = process.platform === 'win32'
    ? ['/d', '/s', '/c', command, ...args]
    : args;

  execFileSync(executable, commandArguments, {
    cwd: tempRoot,
    stdio: 'inherit',
    env: process.env,
  });
};

try {
  writeFileSync(
    join(tempRoot, 'package.json'),
    `${JSON.stringify(
      {
        name: 'dignite-flex-fields-file-explorer-package-smoke',
        private: true,
        version: '0.0.0',
        dependencies: {
          ...workspacePackage.dependencies,
          '@dignite/ng.flex-fields': pathToFileURL(flexFieldsLibDist).href,
          '@dignite/ng.file-explorer': pathToFileURL(fileExplorerLibDist).href,
          '@dignite/ng.flex-fields-file-explorer': pathToFileURL(tarballPath).href,
        },
        devDependencies: {
          typescript: workspacePackage.devDependencies.typescript,
        },
      },
      null,
      2,
    )}\n`,
  );

  writeFileSync(
    join(tempRoot, 'tsconfig.json'),
    `${JSON.stringify(
      {
        compilerOptions: {
          target: 'ES2022',
          module: 'ESNext',
          moduleResolution: 'Bundler',
          strict: true,
          noEmit: true,
          skipLibCheck: true,
        },
        files: ['smoke.ts'],
      },
      null,
      2,
    )}\n`,
  );

  // Compiles the surface a consumer actually touches: the provider they call once, the field-type
  // descriptor it registers, the three host components it wires in, and the configuration shape a
  // field's stored settings have to match.
  writeFileSync(
    join(tempRoot, 'smoke.ts'),
    `import {
  FILE_EXPLORER_FIELD_TYPE,
  FileExplorerConfigComponent,
  FileExplorerConfiguration,
  FileExplorerControlComponent,
  FileExplorerViewComponent,
  provideFileExplorerFieldType,
} from '@dignite/ng.flex-fields-file-explorer';
import type { FieldTypeDefinition } from '@dignite/ng.flex-fields';

const fieldType: FieldTypeDefinition = FILE_EXPLORER_FIELD_TYPE;

export const packageSurface = {
  fieldType,
  components: [FileExplorerConfigComponent, FileExplorerControlComponent, FileExplorerViewComponent],
  configuration: new FileExplorerConfiguration(),
  providers: [provideFileExplorerFieldType()],
};
`,
  );

  run(npmCommand, [
    'install',
    '--ignore-scripts',
    '--legacy-peer-deps',
    '--no-audit',
    '--no-fund',
  ]);

  const installedPackageJsonPath = join(
    tempRoot,
    'node_modules',
    '@dignite',
    'ng.flex-fields-file-explorer',
    'package.json',
  );
  const installedPackage = JSON.parse(readFileSync(installedPackageJsonPath, 'utf8'));
  if (installedPackage.version !== expectedVersion) {
    throw new Error(
      `Installed Angular package version ${installedPackage.version} does not match ${expectedVersion}.`,
    );
  }

  run(npxCommand, ['--no-install', 'tsc', '--project', 'tsconfig.json']);
  console.log(
    `Successfully installed and compiled the flex-fields-file-explorer Angular package at version ${expectedVersion}.`,
  );
}
finally {
  rmSync(tempRoot, { recursive: true, force: true });
}
