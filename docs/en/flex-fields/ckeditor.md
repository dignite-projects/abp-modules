```json
//[doc-seo]
{
    "Description": "The optional CKEditor rich-text field type of FlexFields: HTML or Markdown content, image upload through your own file API, and customizing the editor and the read-only view."
}
```

# CKEditor Field Type

The `CKEditor` field type stores **rich text** edited with [CKEditor 5](https://ckeditor.com), as HTML or, per field, as GitHub Flavored Markdown. It is an optional bolt-on, not a built-in type: an application that never needs rich text never pays for CKEditor's weight.

| Package | Description |
|---|---|
| `Dignite.Abp.FlexFields.CKEditor` | The field type on the server: its configuration and validation. References no rendering or Markdown library. |
| `Dignite.Abp.FlexFields.CKEditor.Web` | The MVC view of a value: Markdown converted to HTML, and sanitized. |
| `@dignite/ng.flex-fields-ckeditor` (npm) | The Angular config, control and view components. |

The field type name is `CKEditor`. Rich text has no typed index slot, so the type is **not searchable**.

## Installation

On the server, install `Dignite.Abp.FlexFields.CKEditor` where the field types are resolved, and depend on `AbpFlexFieldsCKEditorModule`. For an MVC UI, install `Dignite.Abp.FlexFields.CKEditor.Web` instead, which brings the field type and `Dignite.Abp.FlexFields.Web` with it, and depend on `AbpFlexFieldsCKEditorWebModule`.

For an Angular UI:

````bash
npm install @dignite/ng.flex-fields-ckeditor @ckeditor/ckeditor5-angular ckeditor5 marked
````

````ts
import { provideFlexFields } from '@dignite/ng.flex-fields';
import { provideCKEditorFieldType } from '@dignite/ng.flex-fields-ckeditor';

export const appConfig: ApplicationConfig = {
  providers: [provideFlexFields(), provideCKEditorFieldType()],
};
````

CKEditor's stylesheet is loaded at runtime by a fixed name, like the [ng-zorro-antd styles](angular-ui.md#styles); add it to the `styles` of your `angular.json` build target:

````json
{ "input": "node_modules/ckeditor5/dist/ckeditor5.css", "inject": false, "bundleName": "ckeditor5" }
````

Without it, the editor renders as blank space, and the browser console names the entry to add.

The package ships under CKEditor 5's GPL terms. An application with a commercial CKEditor license can register its own field type definition under the `CKEditor` name to replace the components.

## Configuration

| Key | Default | Description |
|---|---|---|
| `CKEditor.Mode` | `Full` (1) | `Basic` (0): a floating toolbar with headings, basic styles, links, lists and code blocks. `Full` (1): a classic toolbar that adds block quotes, tables, image upload, undo/redo and source editing. |
| `CKEditor.ContentFormat` | `Html` (0) | `Html` (0) or `Markdown` (1). Decides what the field stores, in both modes; it is not a runtime toggle. |
| `CKEditor.ImagesContainerName` | `null` | The BLOB container name passed to your upload provider. Without it, there is no image upload button. `Full` mode only. |
| `CKEditor.InitialContent` | `null` | The initial value of a new entity's field. |

The two enumerations are stored as numbers, matching the server's `CKEditorMode` and `CKEditorContentFormat` enums. `CKEditorConfiguration` reads and writes the keys with typed properties.

## Image Upload

The package ships no upload API and no default uploader: inline image upload goes to **your** file API. Implement `CKEditorUploadProvider`, which uploads a `File` into the given container and returns the URL the editor embeds, and register it under the `CKEDITOR_UPLOAD_PROVIDER` token:

````ts
@Injectable({ providedIn: 'root' })
export class MyCKEditorUploadProvider implements CKEditorUploadProvider {
  private readonly restService = inject(RestService);

  upload(file: File, containerName: string): Observable<string> {
    const body = new FormData();
    body.append('file', file, file.name);

    return this.restService
      .request<FormData, { url: string }>(
        { method: 'POST', url: '/api/my-project/files', params: { containerName }, body },
        { apiName: 'MyProject' },
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
````

The image upload button appears only when a provider is registered **and** the field configures `CKEditor.ImagesContainerName`. On the server, the [BLOB Storing module](../blob-storing/index.md) can check the uploaded images, for example with a size limit, an allow-list of image types and a resize.

## Customizing the Editor

Each editor is created with a configuration composed from the field's configuration. To change it, such as adding a plugin, changing the toolbar or setting any other CKEditor 5 option, register a `CKEditorConfigContributor` under the `CKEDITOR_CONFIG_CONTRIBUTORS` multi provider. A contributor receives the composed configuration and a context (the loaded `ckeditor5` package, the `field`, its `mode` and `contentFormat`), and changes the configuration in place or returns another one:

````ts
const withAlignment: CKEditorConfigContributor = (config, { ckeditor5 }) => {
  config.extraPlugins = [...(config.extraPlugins ?? []), ckeditor5.Alignment];
  (config.toolbar as string[]).push('|', 'alignment');
};

providers: [
  { provide: CKEDITOR_CONFIG_CONTRIBUTORS, multi: true, useValue: withAlignment },
]
````

Contributors run once per editor, at creation, in registration order. Take CKEditor plugins from `context.ckeditor5` rather than importing them: the package loads `ckeditor5` lazily, the first time a `CKEditor` field is shown, and a value import of `'ckeditor5'` anywhere in the application would pull it into the main bundle. Type-only imports are fine.

## Customizing the Read-Only View

The read-only view shows the stored HTML, or the HTML a Markdown value converts to, through Angular's sanitized `[innerHTML]`. To change what it shows, register a `CKEditorDisplayContributor` under the `CKEDITOR_DISPLAY_CONTRIBUTORS` multi provider: a function from the HTML to the HTML to display, given a context with the `field` and its `contentFormat`. Only what is displayed changes, never the stored value, and the result is still sanitized.

**Example: Show relative image addresses from the API's host**

````ts
export function showFilesFromApiHost(): CKEditorDisplayContributor {
  const environment = inject(EnvironmentService);

  return html => {
    const apiBase = environment.getApiUrl('MyProject').replace(/\/+$/, '');
    return html.replaceAll('src="/api/my-project/files/', `src="${apiBase}/api/my-project/files/`);
  };
}

providers: [
  { provide: CKEDITOR_DISPLAY_CONTRIBUTORS, multi: true, useFactory: showFilesFromApiHost },
]
````

`useFactory` runs in an injection context, so a contributor can `inject()` what it needs. The editor has the same problem when the admin UI runs on another origin than the API; a `CKEditorConfigContributor` with a small editing-view plugin solves it there, as the package's [README](https://github.com/dignite-projects/abp-modules/blob/main/flex-fields/angular/projects/flex-fields-ckeditor/README.md#customizing-the-editor-configuration) shows.

## See Also

* [Creating a Field Type](custom-field-types.md)
* [Angular UI](angular-ui.md)
