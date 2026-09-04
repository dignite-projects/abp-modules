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
