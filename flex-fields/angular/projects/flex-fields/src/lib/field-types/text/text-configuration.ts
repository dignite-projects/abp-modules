import { TextMode } from './text-mode';

/**
 * Configuration of a `TextEdit` field, shaped for `FormBuilder.group()`. Mirrors `TextConfiguration`
 * on the server.
 *
 * The property names are the **stored** configuration keys, not a naming choice — see
 * `FieldConfigurationDictionary`. The `TextEdit.` prefix is the legacy control name and stays.
 */
export class TextConfiguration {
  'TextEdit.Placeholder': unknown = [''];

  'TextEdit.Mode': unknown = [TextMode.SingleLine];

  // 256 matches the server's default for SingleLine (it uses 1024 for MultipleLine). The old Angular
  // library seeded '265' here, a transposition of 256 that never matched either.
  'TextEdit.CharLimit': unknown = [256];
}
