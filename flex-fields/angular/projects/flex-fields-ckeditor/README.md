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
| `CKEditor.ImagesContainerName` | Blob container the image-upload adapter passes to the host's `CKEDITOR_UPLOAD_PROVIDER` (see [Image upload](#image-upload)). Unset simply omits the upload-image toolbar button. Full mode only — see below; the config designer hides this field and clears any stored value the moment Mode is switched to Basic. |
| `CKEditor.InitialContent` | Seed value for a newly-created field with no stored value yet. |

`Mode`/`ContentFormat` are stored as their **numeric ordinal** (matching the server's
`CKEditorMode`/`CKEditorContentFormat` enums), not their name — see `ckeditor-mode.ts` /
`ckeditor-content-format.ts`.

### Basic vs. Full toolbar

`Basic` is a deliberately lightweight, inline-text-only experience: heading, bold/italic/underline/
strikethrough, link, bulleted/numbered lists, and code block — nothing else, regardless of
`ContentFormat`. `Full` gets the complete set on top of that: blockquote, table, image upload (when
`ImagesContainerName` is configured and the host registered an upload provider), undo/redo, and a `Source` toolbar button (CKEditor 5's
`SourceEditing` plugin, GPL) for viewing/editing the raw stored value directly — HTML, or for a
Markdown-`ContentFormat` field, the raw Markdown text. `SourceEditing` is Full-only regardless: CKEditor
5's own plugin only supports `ClassicEditor` in the first place. `Basic`/`Full` map to CKEditor 5's
`BalloonEditor`/`ClassicEditor` editor classes respectively, but are named for the editing-power
difference that actually matters when choosing a mode, not the editor-shell implementation detail. See
`buildEditorConfig` in `ckeditor-editor-config.ts` for the exact plugin/toolbar composition.

## Image upload

This package ships no upload API and no default uploader. Inline image upload is the **host
application's** to provide: implement `CKEditorUploadProvider` (upload a `File` into the given
container, resolve to the URL the editor should embed) against your own file API and register it under
the `CKEDITOR_UPLOAD_PROVIDER` token:

```ts
@Injectable({ providedIn: 'root' })
export class MyCKEditorUploadProvider implements CKEditorUploadProvider {
  private readonly restService = inject(RestService);

  upload(file: File, containerName: string): Observable<string> {
    const body = new FormData();
    body.append('file', file, file.name);
    return this.restService
      .request<FormData, { url: string }>(
        { method: 'POST', url: '/api/my-app/files', params: { containerName }, body },
        { apiName: 'MyApp' },
      )
      .pipe(map(result => result.url));
  }
}

// app.config.ts
providers: [
  provideFlexFields(),
  provideCKEditorFieldType(),
  { provide: CKEDITOR_UPLOAD_PROVIDER, useExisting: MyCKEditorUploadProvider },
]
```

The upload-image toolbar button appears only when **both** a provider is registered and the field
configures `CKEditor.ImagesContainerName` (Full mode). The provider decides the endpoint, the API name
and how the URL is read from the response; nothing about the wire format is fixed here.

Up to `10.0.0-rc.24` the adapter was hard-wired to `Dignite.FileExplorer`'s `POST /api/file-explorer/files`
(apiName `FileExplorer`). That module has left this repository, so a host that relied on it must now
register a provider for whatever file API it uses.

## Customizing the editor configuration

Each editor is created with the configuration `buildEditorConfig` composes from the field's
configuration keys. To change it — add a plugin, change the toolbar, set any other CKEditor 5
`EditorConfig` option — register a `CKEditorConfigContributor` under the `CKEDITOR_CONFIG_CONTRIBUTORS`
multi provider. A contributor receives the composed configuration and a context (the loaded `ckeditor5`
package, the `field` being edited, its `mode` and `contentFormat`); it either changes the configuration in
place or returns the one to use instead. Contributors run once per editor, at creation, in registration
order; with none registered the configuration is unchanged.

An example. A host stores image addresses relative (`<img src="/api/my-app/files/...">`) but serves its
admin UI from another origin than its API, so in the editor those images would not load. A small plugin
shows them from the API's host in the editing view only — the data pipeline is untouched, so
`getData()`, and with it the form value that is saved, keeps the relative address:

```ts
import { ApplicationConfig, inject } from '@angular/core';
import { EnvironmentService } from '@abp/ng.core';
import type { DowncastAttributeEvent, Editor, ModelElement } from 'ckeditor5';
import { provideFlexFields } from '@dignite/ng.flex-fields';
import {
  CKEDITOR_CONFIG_CONTRIBUTORS,
  CKEditorConfigContributor,
  provideCKEditorFieldType,
} from '@dignite/ng.flex-fields-ckeditor';

const FILE_PATH = '/api/my-app/files/';

export function showFilesFromApiHost(): CKEditorConfigContributor {
  const environment = inject(EnvironmentService);

  return config => {
    const apiBase = environment.getApiUrl('MyApp').replace(/\/+$/, '');

    // A CKEditor 5 plugin can be a plain function of the editor.
    function ShowFilesFromApiHost(editor: Editor): void {
      // 'editingDowncast' only: what the user sees. 'dataDowncast' (getData) keeps the image plugin's
      // own converter, which writes the model's src - the relative address - as it is.
      editor.conversion.for('editingDowncast').add(dispatcher => {
        for (const imageType of ['imageBlock', 'imageInline']) {
          dispatcher.on<DowncastAttributeEvent<ModelElement>>(
            `attribute:src:${imageType}`,
            (evt, data, conversionApi) => {
              // Runs before the image plugin's own src converter (priority 'high') and consumes the
              // change, so that one skips it.
              if (!conversionApi.consumable.consume(data.item, evt.name)) {
                return;
              }

              const src = (data.attributeNewValue as string | null) ?? '';
              const view = conversionApi.mapper.toViewElement(data.item)!;
              const img = editor.plugins.get('ImageUtils').findViewImgElement(view)!;
              conversionApi.writer.setAttribute('src', src.startsWith(FILE_PATH) ? apiBase + src : src, img);
            },
            { priority: 'high' },
          );
        }
      });
    }

    config.extraPlugins = [...(config.extraPlugins ?? []), ShowFilesFromApiHost];
  };
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideFlexFields(),
    provideCKEditorFieldType(),
    // useFactory runs in an injection context, so the contributor can inject() what it needs;
    // a contributor that needs nothing can be registered with useValue.
    { provide: CKEDITOR_CONFIG_CONTRIBUTORS, multi: true, useFactory: showFilesFromApiHost },
  ],
};
```

To add one of CKEditor 5's own plugins, take it from `context.ckeditor5` rather than importing it:

```ts
const withAlignment: CKEditorConfigContributor = (config, { ckeditor5 }) => {
  config.extraPlugins = [...(config.extraPlugins ?? []), ckeditor5.Alignment];
  (config.toolbar as string[]).push('|', 'alignment');
};
```

This package loads `ckeditor5` lazily, the first time a `CKEditor` field is shown; a value import from
`'ckeditor5'` anywhere in the host would pull the whole package into the main bundle. Type-only imports,
as in the example above, are fine.

## License

LGPL-3.0-only. See the [repository](https://github.com/dignite-projects/abp-modules).
