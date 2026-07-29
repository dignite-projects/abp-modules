import { execFileSync } from 'node:child_process';
import { existsSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const [, , tarballArgument, expectedVersion] = process.argv;

if (!tarballArgument || !expectedVersion) {
  throw new Error('Usage: node smoke-test-angular-package.mjs <package.tgz> <expected-version>');
}

const tarballPath = resolve(tarballArgument);
if (!existsSync(tarballPath)) {
  throw new Error(`Angular package tarball does not exist: ${tarballPath}`);
}

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, '..', '..');
const workspacePackage = JSON.parse(
  readFileSync(join(repositoryRoot, 'angular', 'package.json'), 'utf8'),
);
const tempRoot = mkdtempSync(join(tmpdir(), 'dignite-flex-fields-angular-smoke-'));
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
        name: 'dignite-flex-fields-package-smoke',
        private: true,
        version: '0.0.0',
        dependencies: {
          ...workspacePackage.dependencies,
          '@dignite/ng.flex-fields': pathToFileURL(tarballPath).href,
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

  // Compiles the surface a consumer actually touches: the provider they call once, the resolver and
  // registry contract a bolt-on field type implements, the host components they place in a template,
  // and the models their DTOs have to match.
  writeFileSync(
    join(tempRoot, 'smoke.ts'),
    `import {
  BUILT_IN_FIELD_TYPES,
  FieldTypeResolver,
  FlexFieldConfigComponent,
  FlexFieldControlComponent,
  FlexFieldSearchComponent,
  FlexFieldViewComponent,
  FlexFieldQueryOperator,
  FlexFieldValueType,
  provideFlexFields,
  provideFlexFieldTypes,
  readStringList,
} from '@dignite/ng.flex-fields';
import type { FieldTypeDefinition, FlexFieldData, FlexFieldValue } from '@dignite/ng.flex-fields';

const field: FlexFieldData = {
  id: '00000000-0000-0000-0000-000000000000',
  name: 'colour',
  displayName: 'Colour',
  fieldTypeName: 'TextEdit',
  configuration: { 'TextEdit.CharLimit': 256 },
};

const value: FlexFieldValue = { field, required: true, searchable: false, value: 'red' };

const boltOn: FieldTypeDefinition = { name: 'CkEditor', displayNameKey: 'MyApp::FieldType:RichText' };

export const packageSurface = {
  builtInNames: BUILT_IN_FIELD_TYPES.map(fieldType => fieldType.name),
  resolver: FieldTypeResolver,
  components: [
    FlexFieldConfigComponent,
    FlexFieldControlComponent,
    FlexFieldSearchComponent,
    FlexFieldViewComponent,
  ],
  providers: [provideFlexFields(boltOn), provideFlexFieldTypes(boltOn)],
  value,
  operator: FlexFieldQueryOperator.Contains,
  valueType: FlexFieldValueType.String,
  values: readStringList(value.value),
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
    'ng.flex-fields',
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
    `Successfully installed and compiled the flex-fields Angular package at version ${expectedVersion}.`,
  );
}
finally {
  rmSync(tempRoot, { recursive: true, force: true });
}
