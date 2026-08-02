import { FieldTypeDefinition } from '@dignite/ng.flex-fields';
import { FileExplorerConfigComponent } from './file-explorer-config.component';
import { FileExplorerControlComponent } from './file-explorer-control.component';
import { FileExplorerViewComponent } from './file-explorer-view.component';

/**
 * The `FileExplorer` field type: picks one or more files through Dignite.FileExplorer's picker.
 *
 * No search component — the built-ins skip it too when there's no straightforward filter UI (see
 * `DateEdit`); a "field contains file X" search is feature work, not migration.
 *
 * `indexable: false` mirrors the server's `FileExplorerFieldType.IndexValueType => null`: the value
 * stored is an array of file descriptor objects, not a bare scalar the query index has a slot for.
 */
export const FILE_EXPLORER_FIELD_TYPE: FieldTypeDefinition = {
  name: 'FileExplorer',
  displayNameKey: 'FlexFieldsFileExplorer::FieldType:FileExplorer',
  indexable: false,
  configComponent: FileExplorerConfigComponent,
  controlComponent: FileExplorerControlComponent,
  viewComponent: FileExplorerViewComponent,
};
