import { FlexFieldData } from './flex-field-data';

/**
 * One field as it exists at runtime: its definition, how *this* host uses it, and its value.
 * Mirrors `FlexFieldValue` on the server.
 *
 * Keep the three parts separate. `required` and `searchable` are per-**usage**, not per-definition:
 * the same definition attaches to several host types with different rules, so folding the flags into
 * {@link FlexFieldData} would make one host's settings leak into another's.
 */
export interface FlexFieldValue {
  field: FlexFieldData;

  required: boolean;

  searchable: boolean;

  value?: unknown;
}
