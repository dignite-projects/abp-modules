import { EnvironmentProviders } from '@angular/core';
import { provideFlexFieldTypes } from '@dignite/ng.flex-fields';
import { FILE_EXPLORER_FIELD_TYPE } from './file-explorer-field-type';

/**
 * Registers the `FileExplorer` field type. Call alongside `provideFlexFields()`, in your
 * application config:
 *
 * ```ts
 * providers: [provideFlexFields(), provideFileExplorerFieldType()]
 * ```
 */
export function provideFileExplorerFieldType(): EnvironmentProviders {
  return provideFlexFieldTypes(FILE_EXPLORER_FIELD_TYPE);
}
