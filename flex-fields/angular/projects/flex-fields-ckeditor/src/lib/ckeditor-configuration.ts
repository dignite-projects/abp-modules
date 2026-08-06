import { CKEditorContentFormat } from './ckeditor-content-format';
import { CKEditorMode } from './ckeditor-mode';

/**
 * Configuration of a `CKEditor` field, shaped for `FormBuilder.group()`. Mirrors `CKEditorConfiguration`
 * on the server — property names are the **stored** configuration keys, not a naming choice, see
 * `FieldConfigurationDictionary`.
 */
export class CKEditorConfiguration {
  'CKEditor.Mode': unknown = [CKEditorMode.Full];

  'CKEditor.ContentFormat': unknown = [CKEditorContentFormat.Html];

  // No fallback container: unlike FileExplorerConfiguration's FileContainerName, leaving this empty
  // does not make the field unusable - CKEditorControlComponent just omits the image-upload toolbar
  // button (see ckeditor-editor-config.ts).
  'CKEditor.ImagesContainerName': unknown = [''];

  'CKEditor.InitialContent': unknown = [''];
}
