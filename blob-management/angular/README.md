# Blob Management - Angular

This workspace holds two things:

| Path | What it is |
|---|---|
| `src/` | **Host** - the Angular application that runs against `Dignite.Abp.BlobManagement.Host` (local development and demo only, never published). It consumes the library from source through the `paths` in `tsconfig.json`. |
| `projects/blob-management/` | **`@dignite/ng.blob-management`** - the publishable Angular library: the management page and its parts, services and the proxies, with the route and menu configuration in its secondary entry point `@dignite/ng.blob-management/config`. |
| `projects/dev-app/` | An environment stub (`environment.ts` only, never built) that the ABP schematics read when generating the **library's** proxies: it carries the `Dignite.Abp.BlobManagement` root namespace, which `Host` (`Dignite.Abp.BlobManagement.Host`) does not. See [Generate Proxy](#generate-proxy). |

For more information about the framework, visit [abp.io](https://abp.io/).

## Pre-requirements

* [Node.js](https://nodejs.org/) `^22.22.3`, `^24.15.0` or `>=26` (Angular 22's requirement; CI uses the version in the repository's `.nvmrc`)
* [npm](https://www.npmjs.com/) or [yarn](https://yarnpkg.com/)

## Getting Started

### Install dependencies

```bash
npm install
```

### Install ABP client-side libraries

If you haven't already, run the following command from the `host` folder:

```bash
abp install-libs
```

### Start the backend

Before running the Angular application, make sure your backend API is running. Start the `Dignite.Abp.BlobManagement.Host` application.

## Development server

Run `ng serve` or `npm start` for a dev server. Navigate to `http://localhost:4200/`. The app will automatically reload if you change any of the source files.

## ABP CLI Commands

### Generate Proxy

Generate TypeScript proxies for your backend APIs:

```bash
abp generate-proxy -t ng
```

This command generates service proxies that allow you to call your backend APIs in a type-safe manner. Without `--source` it reads the root namespace and API URL from the first application in `angular.json`, which is `Host`.

To generate the **library's** proxies, name `dev-app` as the source and `blob-management` as the target, so that types in the `Dignite.Abp.BlobManagement.*` namespace are laid out as `proxy/<sub-namespace>/...` instead of `proxy/dignite/blob-management/<sub-namespace>/...` (the root namespace is only stripped from a namespace that starts with it, and `Host`'s `Dignite.Abp.BlobManagement.Host` does not):

```bash
abp generate-proxy -t ng -m blobManagement --source dev-app --target blob-management
```

`dev-app` points at the Host API (`https://localhost:44352`), so start `Dignite.Abp.BlobManagement.Host` first. Keep `Host` the first application in `angular.json`: the ABP Studio run profile generates the Host's own proxies without `--source` and relies on that order.

## Code scaffolding

Run `ng generate component component-name` to generate a new component. You can also use `ng generate directive|pipe|service|class|guard|interface|enum|module`. To scaffold into the library, add `--project blob-management`.

## Build

### The Host application

Run `npm run build` to build the Host application. The build artifacts will be stored in the `dist/Host` directory.

For a production build:

```bash
npm run build:prod
```

### The library

```bash
npm run build:lib
```

The build artifacts will be stored in the `dist/blob-management` directory. For development with automatic rebuilds, run `ng build blob-management --watch`.

## Running unit tests

Run `npm test` (`ng test blob-management`) to execute the library's unit tests via [Vitest](https://vitest.dev/).

## Publishing the library

After building the library, publish it to npm:

```bash
cd dist/blob-management
npm publish
```

## Environment Configuration

### Development Environment

The development environment configuration is located in `src/environments/environment.ts`. Update the `apiUrl` to point to your backend API.

### Production Environment

For production, the application uses dynamic environment configuration through the `dynamic-env.json` file. This allows you to configure the environment variables at deployment time without rebuilding the application.

See [Environment](https://abp.io/docs/latest/framework/ui/angular/environment) for more information.

## Additional Resources

* [ABP Angular UI Documentation](https://abp.io/docs/latest/framework/ui/angular/overview)
* [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli)
* [Angular Library Development](https://angular.dev/tools/libraries)
