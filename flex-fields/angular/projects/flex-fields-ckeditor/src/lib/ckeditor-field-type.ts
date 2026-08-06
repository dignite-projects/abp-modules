import { FieldTypeDefinition } from '@dignite/ng.flex-fields';
import { CKEditorConfigComponent } from './ckeditor-config.component';
import { CKEditorControlComponent } from './ckeditor-control.component';
import { CKEditorViewComponent } from './ckeditor-view.component';

/**
 * The `CKEditor` field type: rich text edited with CKEditor 5 (https://ckeditor.com).
 *
 * No search component - `CKEditorFieldType.IndexValueType` is null on the server, so a field of this
 * type can never be marked `Searchable`; restating that here would be exactly the drift
 * `FieldTypeDefinition`'s own doc warns about.
 */
export const CKEDITOR_FIELD_TYPE: FieldTypeDefinition = {
  name: 'CKEditor',
  displayNameKey: 'FlexFieldsCKEditor::FieldType:CKEditor',
  configComponent: CKEditorConfigComponent,
  controlComponent: CKEditorControlComponent,
  viewComponent: CKEditorViewComponent,
};
