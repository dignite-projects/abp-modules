# @dignite/ng.flex-fields-file-explorer

`FileExplorer` field type for **[@dignite/ng.flex-fields](https://github.com/dignite-projects/abp-modules/tree/main/flex-fields)** —
picks one or more files through **[Dignite.FileExplorer](https://github.com/dignite-projects/abp-modules/tree/main/file-storing)**'s
picker.

> **Angular 21 · ABP 10.5 · LGPL-3.0-only**

A bolt-on, not a built-in: flex-fields ships with no knowledge of file-storing, so consumers who
never need a file field never pay for `@dignite/ng.file-explorer`'s dependency weight. Install this
package only if you do.

## Install

```bash
npm install @dignite/ng.flex-fields-file-explorer @dignite/ng.file-explorer
```

### Styles

This package declares no stylesheet of its own. It renders `@dignite/ng.file-explorer`'s picker,
whose `ngx-datatable` CSS every ABP Angular host already bundles — the three
`@swimlane/ngx-datatable` entries the ABP startup template puts in `angular.json` — and whose
`<abp-tree>` loads `ng-zorro-antd-tree.css` itself, by bundle name.

So the `ng-zorro-antd-tree` entry described in the
[core package's README](https://github.com/dignite-projects/abp-modules/blob/main/flex-fields/angular/projects/flex-fields/README.md#styles) is the one thing to check here; it is the
same entry the `Tree` field types already need, so a host that has flex-fields' built-in types
working has nothing to add for this package.

## Usage

Register it alongside the built-ins, in your application config:

```ts
import { provideFlexFields } from '@dignite/ng.flex-fields';
import { provideFileExplorerFieldType } from '@dignite/ng.flex-fields-file-explorer';

export const appConfig: ApplicationConfig = {
  providers: [provideFlexFields(), provideFileExplorerFieldType()],
};
```

The registration key is `FileExplorer` — a field's `fieldTypeName` must match a server-side
`FieldTypeBase` registered under the same string for the round trip to work; this package is the
Angular half only.

## Configuration keys

| Key | Meaning |
|---|---|
| `FileExplorer.FileContainerName` | Blob container the picker browses/uploads to. Empty defers to the picker's own `Images` default. |
| `FileExplorer.UploadFileMultiple` | Whether more than one file may be selected. |

These are carried over unchanged from the migrated dignite-abp implementation, so fields configured
there keep working.

## License

LGPL-3.0-only. See the [repository](https://github.com/dignite-projects/abp-modules).
