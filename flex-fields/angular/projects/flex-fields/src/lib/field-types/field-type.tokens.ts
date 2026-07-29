import { InjectionToken } from '@angular/core';
import { FieldTypeDefinition } from './field-type-definition';

/**
 * Registered field types, as a multi-provider: each contributor supplies one array and
 * {@link FieldTypeResolver} flattens them.
 *
 * Multi rather than a single value so that several packages — the built-ins, a CkEditor bolt-on, a
 * FileExplorer bolt-on, the host application — can each register independently without any of them
 * having to know about the others.
 */
export const FLEX_FIELD_TYPES = new InjectionToken<FieldTypeDefinition[][]>(
  'FLEX_FIELD_TYPES',
);
