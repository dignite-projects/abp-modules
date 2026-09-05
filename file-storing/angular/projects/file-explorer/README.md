# @dignite/ng.file-explorer

Angular UI for **[Dignite.FileExplorer](https://github.com/dignite-projects/abp-modules/tree/main/file-storing)** —
an upload widget, a directory-tree file picker, and the ABP-generated API proxies over
`/api/file-explorer`.

> **Angular 21 · ABP 10.5 · LGPL-3.0-only**

## Install

```bash
npm install @dignite/ng.file-explorer
```

Register the `/config` secondary entry point once, in your application config, to wire up its
routes:

```ts
import { provideFileExplorerConfig } from '@dignite/ng.file-explorer/config';

export const appConfig: ApplicationConfig = {
  providers: [provideFileExplorerConfig()],
};
```

### Peer dependencies

`@abp/ng.components` is a **dependency** of this package, not a peer — you do not declare it, and it
is installed for you. It is deliberately not a peer: an ABP Angular host is not guaranteed to have it
(neither `@abp/ng.core` nor `@abp/ng.theme.shared` depends on it — only feature packages such as
`@abp/ng.identity` do), so asking the consumer for it left this package's `@abp/ng.components/tree`
import resolving to nothing on any install that skips peers, `--legacy-peer-deps` included.

Everything in `peerDependencies` you **must** declare yourself, including `@angular/cdk` (`~21.2.0`),
which the picker imports directly for drag-and-drop and which no stock ABP host brings in on its own.
It reaches this package only as `@abp/ng.components` → `ng-zorro-antd` → `@angular/cdk`, and the
middle link is pinned: `@abp/ng.components` requires `ng-zorro-antd` `~21.0.0-next.1`, i.e. `<21.1.0`.
If your host also declares `ng-zorro-antd` at a wider range — `^21.0.2`, or the `21.3.3` current hosts
run — no single version satisfies both, so your package manager installs two: yours at the root and
`21.0.2` nested under `@abp/ng.components`. Two copies are two module-scoped `NZ_CONFIG` /
`NzConfigService` injection tokens, so `provideNzConfig()` and `provideNzI18n()` will not reach the
`abp-tree` inside the directory tree. Pin inside `<21.1.0` if you need a single copy.

## Components

| Selector | Role |
|---|---|
| `fe-file-explorer-upload` | Upload widget for a single blob container |
| `fe-file-explorer-picker` | Shows the currently selected file(s); opens the modal to add more |
| `fe-file-explorer-modal` | Directory tree + file table + inline upload, used by the picker |
| `fe-file-explorer-directory-tree` | Standalone directory navigation tree |

```html
<fe-file-explorer-upload
  [multiple]="false"
  fileContainerName="SampleContainer"
  (fileDataChange)="onFileDataChange($event)"
></fe-file-explorer-upload>

<fe-file-explorer-picker
  [multiple]="true"
  [selectFormFile]="selectedFileGroup"
  fileContainerName="SampleContainer"
  (selectedFileChange)="onSelectedFileChange($event)"
></fe-file-explorer-picker>
```

`fileContainerName` has no default — an unconfigured container is a configuration bug, not silently
treated as any particular one, and must match a blob container configured server-side via
`Dignite.Abp.FileStoring`.

## License

LGPL-3.0-only. See the [repository](https://github.com/dignite-projects/abp-modules).
