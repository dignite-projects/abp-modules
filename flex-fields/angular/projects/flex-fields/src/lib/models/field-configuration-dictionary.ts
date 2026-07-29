/**
 * A field type's configuration, as stored. Mirrors `FieldConfigurationDictionary` on the server.
 *
 * The keys are persisted strings, not property names — `TextEdit.CharLimit`,
 * `NumericEditField.Decimals`, and so on. They are data: they did not follow the C# class rename
 * from `*FormControl` to `*FieldType`, and changing one here would silently orphan every value
 * already written under the old key.
 */
export type FieldConfigurationDictionary = Record<string, unknown>;
