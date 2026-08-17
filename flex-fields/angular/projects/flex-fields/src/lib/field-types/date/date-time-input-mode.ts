/**
 * What a date field lets the user enter. Mirrors the `DateTimeInputMode` enum on the server — the
 * ordinals are the stored value of `DateTime.InputMode`.
 */
export enum DateTimeInputMode {
  /** Only a date. */
  Date = 0,

  /** Both date and time. */
  DateTime = 1,

  /** Year and month only. */
  Month = 2,
}
