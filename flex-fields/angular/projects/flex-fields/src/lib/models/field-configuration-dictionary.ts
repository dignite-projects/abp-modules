/**
 * A field type's configuration, as stored. Mirrors `FieldConfigurationDictionary` on the server.
 *
 * The keys are persisted strings, not property names — `Text.CharLimit`, `Number.Decimals`, and so
 * on. They are data: changing one here without a matching change on the server would silently
 * orphan every value already written under the old key.
 */
export type FieldConfigurationDictionary = Record<string, unknown>;
