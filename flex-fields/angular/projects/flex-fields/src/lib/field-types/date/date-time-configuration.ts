import { DateTimeInputMode } from './date-time-input-mode';

/**
 * Configuration of a `DateEdit` field, shaped for `FormBuilder.group()`. Mirrors
 * `DateTimeConfiguration` on the server.
 *
 * The stored keys are `DateEdit.*`, not `DateTime.*` — `DateEdit` is the persisted registration key
 * and the configuration keys are built from it, independent of what the class itself is named.
 */
export class DateTimeConfiguration {
  'DateEdit.InputMode': unknown = [DateTimeInputMode.Date];

  'DateEdit.Min': unknown = [''];

  'DateEdit.Max': unknown = [''];
}

/** The `<input type>` and date format each input mode uses. */
export const DATE_INPUT_MODE_FORMATS: Readonly<
  Record<DateTimeInputMode, { inputType: string; format: string }>
> = {
  [DateTimeInputMode.Date]: { inputType: 'date', format: 'yyyy-MM-dd' },
  [DateTimeInputMode.DateTime]: { inputType: 'datetime-local', format: 'yyyy-MM-dd HH:mm:ss' },
  [DateTimeInputMode.Month]: { inputType: 'month', format: 'yyyy-MM' },
};
