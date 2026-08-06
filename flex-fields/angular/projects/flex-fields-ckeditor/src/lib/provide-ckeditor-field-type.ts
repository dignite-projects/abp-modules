import { EnvironmentProviders } from '@angular/core';
import { provideFlexFieldTypes } from '@dignite/ng.flex-fields';
import { CKEDITOR_FIELD_TYPE } from './ckeditor-field-type';

/**
 * Registers the `CKEditor` field type. Call alongside `provideFlexFields()`, in your application
 * config:
 *
 * ```ts
 * providers: [provideFlexFields(), provideCKEditorFieldType()]
 * ```
 */
export function provideCKEditorFieldType(): EnvironmentProviders {
  return provideFlexFieldTypes(CKEDITOR_FIELD_TYPE);
}
