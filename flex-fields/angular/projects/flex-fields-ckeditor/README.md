# @dignite/ng.flex-fields-ckeditor

`CKEditor` field type for **[@dignite/ng.flex-fields](https://github.com/dignite-projects/abp-modules/tree/main/flex-fields)** —
rich text edited with **[CKEditor 5](https://ckeditor.com)**, persisted as HTML or, per field, GitHub
Flavored Markdown.

> **Angular 21 · ABP 10.5 · CKEditor 5 · LGPL-3.0-only**

A bolt-on, not a built-in: flex-fields ships with no knowledge of CKEditor, so consumers who never
need a rich-text field never pay for its dependency weight. Install this package only if you do.

## Install

```bash
npm install @dignite/ng.flex-fields-ckeditor @ckeditor/ckeditor5-angular ckeditor5 marked
```

### Styles

CKEditor 5's UI stylesheet is **served by your host under a fixed name** and fetched the first time a
`CKEditor` field is rendered, rather than compiled into this package. One entry in the `styles` array
of your `angular.json` build target:

```json
{ "input": "node_modules/ckeditor5/dist/ckeditor5.css", "inject": false, "bundleName": "ckeditor5" }
```

It is the `ckeditor5` you just installed: this package's editor JavaScript also comes from your copy,
at runtime, so declaring the CSS the same way keeps the two halves on one version instead of pinning
the stylesheet to whatever version this package was built against. `inject: false` is required, not a
preference — an injected entry is emitted under a content hash in a production build, which no fixed
name can find; the [core package's README](https://github.com/dignite-projects/abp-modules/blob/main/flex-fields/angular/projects/flex-fields/README.md#styles) has the full explanation,
and that section's `ng-zorro-antd-*` entries apply on top of this one if you also use the built-in
field types.

Without the entry the editor's DOM is still built, but CKEditor's layout never arrives, so the field
renders as blank/collapsed space — and the browser console carries one error naming the file and
quoting the entry to add. `DISABLE_FLEX_FIELDS_STYLE_LOADING_TOKEN` (from `@dignite/ng.flex-fields`)
switches the loading off for an application that already bundles this CSS some other way; it is
family-wide, so `true` silences the sibling packages' bundles too.

## Usage

Register it alongside the built-ins, in your application config:

```ts
import { provideFlexFields } from '@dignite/ng.flex-fields';
import { provideCKEditorFieldType } from '@dignite/ng.flex-fields-ckeditor';

export const appConfig: ApplicationConfig = {
  providers: [provideFlexFields(), provideCKEditorFieldType()],
};
```

The registration key is `CKEditor` — a field's `fieldTypeName` must match a server-side
`FieldTypeBase` registered under the same string for the round trip to work; this package is the
Angular half only.

This package ships under CKEditor 5's GPL terms (`licenseKey: 'GPL'`, set internally). A host that
holds a commercial CKEditor license and wants those terms instead can register its own
`FieldTypeDefinition` under the same `'CKEditor'` name — a later registration overrides an earlier one
(see `FieldTypeResolver`).

## Configuration keys

| Key | Meaning |
|---|---|
| `CKEditor.Mode` | `Basic` (0, `BalloonEditor` — floating toolbar on selection, no persistent toolbar bar) or `Full` (1, `ClassicEditor`). Default `Full`. Named for editing power, not the underlying CKEditor 5 editor class — see below. |
| `CKEditor.ContentFormat` | `Html` (0) or `Markdown` (1, GitHub Flavored). Default `Html`. Decided once, at editor-creation time — not a runtime toggle (see `ckeditor-editor-config.ts`). Applies in both Mode values — it's about which data format the field stores, not which toolbar buttons show. |
| `CKEditor.ImagesContainerName` | Blob container the image-upload adapter posts to, via `Dignite.FileExplorer`'s existing upload API. Unset simply omits the upload-image toolbar button. Full mode only — see below; the config designer hides this field and clears any stored value the moment Mode is switched to Basic. |
| `CKEditor.InitialContent` | Seed value for a newly-created field with no stored value yet. |

`Mode`/`ContentFormat` are stored as their **numeric ordinal** (matching the server's
`CKEditorMode`/`CKEditorContentFormat` enums), not their name — see `ckeditor-mode.ts` /
`ckeditor-content-format.ts`.

### Basic vs. Full toolbar

`Basic` is a deliberately lightweight, inline-text-only experience: heading, bold/italic/underline/
strikethrough, link, bulleted/numbered lists, and code block — nothing else, regardless of
`ContentFormat`. `Full` gets the complete set on top of that: blockquote, table, image upload (when
`ImagesContainerName` is configured), undo/redo, and a `Source` toolbar button (CKEditor 5's
`SourceEditing` plugin, GPL) for viewing/editing the raw stored value directly — HTML, or for a
Markdown-`ContentFormat` field, the raw Markdown text. `SourceEditing` is Full-only regardless: CKEditor
5's own plugin only supports `ClassicEditor` in the first place. `Basic`/`Full` map to CKEditor 5's
`BalloonEditor`/`ClassicEditor` editor classes respectively, but are named for the editing-power
difference that actually matters when choosing a mode, not the editor-shell implementation detail. See
`buildEditorConfig` in `ckeditor-editor-config.ts` for the exact plugin/toolbar composition.

## License

LGPL-3.0-only. See the [repository](https://github.com/dignite-projects/abp-modules).
