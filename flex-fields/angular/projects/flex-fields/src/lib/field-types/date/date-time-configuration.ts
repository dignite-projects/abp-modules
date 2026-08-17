import { DateTimeInputMode } from './date-time-input-mode';

/**
 * Configuration of a `DateTime` field, shaped for `FormBuilder.group()`. Mirrors
 * `DateTimeConfiguration` on the server.
 */
export class DateTimeConfiguration {
  'DateTime.InputMode': unknown = [DateTimeInputMode.Date];

  'DateTime.Min': unknown = [''];

  'DateTime.Max': unknown = [''];
}

/** The `<input type>` and date format each input mode uses. */
export const DATE_INPUT_MODE_FORMATS: Readonly<
  Record<DateTimeInputMode, { inputType: string; format: string }>
> = {
  [DateTimeInputMode.Date]: { inputType: 'date', format: 'yyyy-MM-dd' },
  [DateTimeInputMode.DateTime]: { inputType: 'datetime-local', format: 'yyyy-MM-dd HH:mm:ss' },
  [DateTimeInputMode.Month]: { inputType: 'month', format: 'yyyy-MM' },
};
