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
const tempRoot = mkdtempSync(join(tmpdir(), 'dignite-notifications-angular-smoke-'));
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
        name: 'dignite-notification-center-package-smoke',
        private: true,
        version: '0.0.0',
        dependencies: {
          ...workspacePackage.dependencies,
          '@dignite/ng.notification-center': pathToFileURL(tarballPath).href,
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

  writeFileSync(
    join(tempRoot, 'smoke.ts'),
    `import {
  NotificationBellComponent,
  NotificationSubscriptionsComponent,
  UserNotificationService,
  NotificationSubscriptionService,
} from '@dignite/ng.notification-center';
import {
  eNotificationCenterRouteNames,
  provideNotificationCenterConfig,
} from '@dignite/ng.notification-center/config';

export const packageSurface = {
  NotificationBellComponent,
  NotificationSubscriptionsComponent,
  UserNotificationService,
  NotificationSubscriptionService,
  provideNotificationCenterConfig,
  notificationsRoute: eNotificationCenterRouteNames.Notifications,
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
    'ng.notification-center',
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
    `Successfully installed and compiled both Angular package entry points at version ${expectedVersion}.`,
  );
}
finally {
  rmSync(tempRoot, { recursive: true, force: true });
}
