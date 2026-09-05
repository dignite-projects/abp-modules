import { Type } from '@angular/core';

/**
 * What the library needs to know to render one field type. The client-side counterpart of
 * `IFieldType` on the server — but note the two carry different halves of the concept: the server's
 * `IFieldType` has no rendering member at all (that is the whole point of the `FormControl` →
 * `FieldType` rename), and this has no validation or indexing member. They meet at {@link name}.
 *
 * **Rendering only, deliberately.** Anything the server decides — most of all whether a type's values
 * are indexable, i.e. whether a field of it can be marked searchable at all — belongs to the server
 * and has to reach the client from it, not from a copy declared here. `IFieldType.IndexValueType` is
 * the only source of that answer; a second one sitting in this file would be a hand-maintained mirror
 * that neither build could catch drifting. The kernel ships no application layer, so serving it is a
 * downstream's job — see the demo's `ProductFieldAppService.GetFieldTypesAsync` and `FieldTypeDto`.
 */
export interface FieldTypeDefinition {
  /**
   * Registration key. Must equal the server field type's `Name` — `Text`, `Number`,
   * `DateTime`, `Select`, `Boolean`, `Tree`, `Matrix`, `Table` for the built-ins.
   *
   * These are stored values, not class names. `Text` is served by `TextFieldType` on the server
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

  /**
   * Whether this type's own configuration declares further fields — the client-side counterpart of
   * the server's `ICompositeFieldType`. `Matrix` and `Table` are the built-ins that set it.
   *
   * Declared by the same package that ships the type, so it is not a host-maintained mirror the way a
   * copy of `IndexValueType` would be — the package that owns `MatrixConfigComponent` is the same one
   * that knows a Matrix has sub-fields. It is used for exactly one thing: letting a composite config
   * editor stop *offering* composite types once {@link MAX_COMPOSITE_NESTING_DEPTH} is reached, rather
   * than letting an admin build a definition the save will reject. The server's
   * `CompositeFieldNesting` remains the authority and refuses an over-deep definition regardless of
   * what this says.
   *
   * This does not contradict the "rendering only" rule above: whether a config editor recurses is a
   * client rendering fact, not a server decision restated here.
   */
  composite?: boolean;
}
