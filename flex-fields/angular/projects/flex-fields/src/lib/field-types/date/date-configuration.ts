import { DateInputMode } from './date-input-mode';

/**
 * Configuration of a `DateEdit` field, shaped for `FormBuilder.group()`. Mirrors `DateConfiguration`
 * on the server.
 *
 * Named after the folder rather than the component, as on the server: the field type class is
 * `DateTimeFieldType` but its configuration is `DateConfiguration`, because `DateEdit` is the stored
 * registration key the configuration keys are built from.
 */
export class DateConfiguration {
  'DateEdit.InputMode': unknown = [DateInputMode.Date];

  'DateEdit.Min': unknown = [''];

  'DateEdit.Max': unknown = [''];
}

/** The `<input type>` and date format each input mode uses. */
export const DATE_INPUT_MODE_FORMATS: Readonly<
  Record<DateInputMode, { inputType: string; format: string }>
> = {
  [DateInputMode.Date]: { inputType: 'date', format: 'yyyy-MM-dd' },
  [DateInputMode.DateTime]: { inputType: 'datetime-local', format: 'yyyy-MM-dd HH:mm:ss' },
  [DateInputMode.Month]: { inputType: 'month', format: 'yyyy-MM' },
};
