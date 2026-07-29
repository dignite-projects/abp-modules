/**
 * What a date field lets the user enter. Mirrors the `DateInputMode` enum on the server — the
 * ordinals are the stored value of `DateEdit.InputMode`.
 */
export enum DateInputMode {
  /** Only a date. */
  Date = 0,

  /** Both date and time. */
  DateTime = 1,

  /** Year and month only. */
  Month = 2,
}
