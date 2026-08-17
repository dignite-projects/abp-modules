/**
 * Configuration of a `Number` field, shaped for `FormBuilder.group()`. Mirrors `NumberConfiguration`
 * on the server.
 *
 * One stored-key quirk is deliberate and matches the server exactly: `FormatSpecifier` carries no
 * `Number.` prefix at all, unlike every other key here. That is a persisted key — tidying it here
 * would orphan stored configurations.
 */
export class NumberConfiguration {
  'Number.Placeholder': unknown = [''];

  // Optional, matching the server's `decimal? Min` / `decimal? Max`. The old Angular library put
  // Validators.required on both, which made every numeric field unconfigurable without bounds.
  'Number.Min': unknown = [null];

  'Number.Max': unknown = [null];

  'Number.Decimals': unknown = [2];

  'Number.Step': unknown = [null];

  'FormatSpecifier': unknown = [''];
}
