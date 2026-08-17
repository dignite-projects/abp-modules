import { FieldConfigurationDictionary } from './field-configuration-dictionary';

/**
 * A flex field's **definition**: identity, the field type it is bound to, and that field type's
 * configuration. Mirrors `IFlexFieldData` on the server.
 *
 * A definition carries no value and no per-usage flags — the same field can be attached to several
 * host types with different Required/Searchable settings. Definition + those flags + the value read
 * from the host's bag are merged at runtime into a {@link FlexFieldValue}.
 */
export interface FlexFieldData {
  id: string;

  /**
   * Unique name of the field, and the key its value is stored under in the host's value bag.
   */
  name: string;

  displayName: string;

  description?: string;

  /**
   * Registration key of the field type this field is bound to — `Text`, `Select`, `Tree`, …
   *
   * This is a stored value, not a class name: `Text` is served by `TextFieldType` on the server
   * and by `TextControlComponent` here.
   */
  fieldTypeName: string;

  configuration: FieldConfigurationDictionary;
}
