import { FieldTypeDefinition } from '@dignite/ng.flex-fields';
import { FileExplorerConfigComponent } from './file-explorer-config.component';
import { FileExplorerControlComponent } from './file-explorer-control.component';
import { FileExplorerViewComponent } from './file-explorer-view.component';

/**
 * The `FileExplorer` field type: picks one or more files through Dignite.FileExplorer's picker.
 *
 * No search component — the built-ins skip it too when there's no straightforward filter UI (see
 * `DateTime`); a "field contains file X" search is feature work, not migration.
 *
 * That this type is also *not indexable* — `FileExplorerFieldType.IndexValueType` is null, so a field
 * of it can never be marked searchable — is deliberately not restated here. The server owns that
 * answer and a downstream serves it to the client (the demo's `GET /api/app/product-field/field-types`);
 * declaring it a second time in this file is exactly the drift `FieldTypeDefinition`'s doc warns about.
 */
export const FILE_EXPLORER_FIELD_TYPE: FieldTypeDefinition = {
  name: 'FileExplorer',
  displayNameKey: 'FlexFieldsFileExplorer::FieldType:FileExplorer',
  configComponent: FileExplorerConfigComponent,
  controlComponent: FileExplorerControlComponent,
  viewComponent: FileExplorerViewComponent,
};
