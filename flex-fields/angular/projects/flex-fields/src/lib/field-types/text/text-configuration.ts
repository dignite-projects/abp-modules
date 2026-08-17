import { TextMode } from './text-mode';

/**
 * Configuration of a `Text` field, shaped for `FormBuilder.group()`. Mirrors `TextConfiguration`
 * on the server.
 *
 * The property names are the **stored** configuration keys, not a naming choice — see
 * `FieldConfigurationDictionary`.
 */
export class TextConfiguration {
  'Text.Placeholder': unknown = [''];

  'Text.Mode': unknown = [TextMode.SingleLine];

  // 256 matches the server's default for SingleLine (it uses 1024 for MultipleLine). The old Angular
  // library seeded '265' here, a transposition of 256 that never matched either.
  'Text.CharLimit': unknown = [256];
}
