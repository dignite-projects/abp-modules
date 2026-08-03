/**
 * Configuration of a `NumericEdit` field, shaped for `FormBuilder.group()`. Mirrors
 * `NumberConfiguration` on the server.
 *
 * Two stored-key quirks are deliberate and match the server exactly: the prefix is
 * `NumericEditField.` (not `NumericEdit.`, unlike every other type), and `FormatSpecifier` carries no
 * prefix at all. Both are persisted keys — tidying them here would orphan stored configurations.
 */
export class NumberConfiguration {
  'NumericEditField.Placeholder': unknown = [''];

  // Optional, matching the server's `decimal? Min` / `decimal? Max`. The old Angular library put
  // Validators.required on both, which made every numeric field unconfigurable without bounds.
  'NumericEditField.Min': unknown = [null];

  'NumericEditField.Max': unknown = [null];

  'NumericEditField.Decimals': unknown = [2];

  'NumericEditField.Step': unknown = [null];

  'FormatSpecifier': unknown = [''];
}
