import { Type } from '@angular/core';

/**
 * What the library needs to know to render one field type. The client-side counterpart of
 * `IFieldType` on the server — but note the two carry different halves of the concept: the server's
 * `IFieldType` has no rendering member at all (that is the whole point of the `FormControl` →
 * `FieldType` rename), and this has no validation or indexing member. They meet at {@link name}.
 */
export interface FieldTypeDefinition {
  /**
   * Registration key. Must equal the server field type's `Name` — `TextEdit`, `NumericEdit`,
   * `DateEdit`, `Select`, `Switch`, `TreeView` for the built-ins.
   *
   * These are stored values, not class names. `TextEdit` is served by `TextFieldType` on the server
   * and `TextControlComponent` here; renaming the key would orphan every field already bound to it.
   */
  name: string;

  /**
   * Localization key for the human-readable name, e.g. `FlexFields::FieldType:Text`. Resolved through
   * the ABP localization pipe, so it matches whatever the server's `DisplayName` renders.
   */
  displayNameKey: string;

  /** Designs the field — the admin-side editor for this type's configuration. */
  configComponent?: Type<unknown>;

  /** Edits a value. */
  controlComponent?: Type<unknown>;

  /** Displays a value read-only. */
  viewComponent?: Type<unknown>;

  /** Filters by the field. Absent when the type has no meaningful search UI. */
  searchComponent?: Type<unknown>;
}
