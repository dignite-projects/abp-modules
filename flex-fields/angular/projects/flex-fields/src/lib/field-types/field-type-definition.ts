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

  /**
   * Mirrors the server's `IFieldType.IndexValueType != null` — whether a value of this type can ever
   * be written to the query index, regardless of a particular field's `Searchable` setting. `false` for
   * a type like `FileExplorer` that stores something with no sensible typed index slot (an array of
   * file descriptors, not a bare scalar).
   *
   * A downstream's own field-admin UI (Required/Searchable are its own concern, not a library
   * component — see `FieldTypeDefinition`'s file doc) should read this to disable or hide its
   * "Searchable" control: toggling it on for a non-indexable type is accepted but silently has no
   * effect, since the index manager skips these fields regardless of `Searchable`.
   */
  indexable: boolean;

  /** Designs the field — the admin-side editor for this type's configuration. */
  configComponent?: Type<unknown>;

  /** Edits a value. */
  controlComponent?: Type<unknown>;

  /** Displays a value read-only. */
  viewComponent?: Type<unknown>;

  /** Filters by the field. Absent when the type has no meaningful search UI. */
  searchComponent?: Type<unknown>;
}
