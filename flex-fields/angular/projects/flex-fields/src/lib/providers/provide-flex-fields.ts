import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { BUILT_IN_FIELD_TYPES } from '../field-types/built-in-field-types';
import { FieldTypeDefinition } from '../field-types/field-type-definition';
import { FLEX_FIELD_TYPES } from '../field-types/field-type.tokens';

/**
 * Registers the six built-in field types, plus any extras you pass. Call once, in your application
 * config:
 *
 * ```ts
 * providers: [provideFlexFields()]
 * ```
 *
 * Extras are registered after the built-ins, so passing a definition named `Text` replaces the
 * built-in text field type rather than colliding with it.
 */
export function provideFlexFields(...fieldTypes: FieldTypeDefinition[]): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: FLEX_FIELD_TYPES, useValue: [...BUILT_IN_FIELD_TYPES], multi: true },
    ...(fieldTypes.length > 0
      ? [{ provide: FLEX_FIELD_TYPES, useValue: fieldTypes, multi: true }]
      : []),
  ]);
}

/**
 * Registers field types **without** the built-ins. This is what a bolt-on package wraps to export its
 * own `provideXxxFieldType()`, so a host can add it without that package having to re-register — or
 * accidentally re-order — anything the library already provides.
 */
export function provideFlexFieldTypes(...fieldTypes: FieldTypeDefinition[]): EnvironmentProviders {
  return makeEnvironmentProviders([
    { provide: FLEX_FIELD_TYPES, useValue: fieldTypes, multi: true },
  ]);
}
