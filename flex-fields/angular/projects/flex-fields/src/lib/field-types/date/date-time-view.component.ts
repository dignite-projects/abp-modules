import { Component, inject, Input } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { DatePipe } from '@angular/common';
import { FlexFieldValue } from '../../models';
import { DATE_INPUT_MODE_FORMATS } from './date-time-configuration';
import { DateTimeInputMode } from './date-time-input-mode';

/**
 * Displays the value of a `DateTime` field read-only, formatted per its `DateTime.InputMode`
 * configuration (mirroring `date-time-control.component.ts`'s edit-mode formatting). Falls back to
 * the user's short date-time format when no configuration is available to read a mode from.
 */
@Component({
  selector: 'ff-date-time-view',
  templateUrl: './date-time-view.component.html',
  imports: [CoreModule],
  providers: [DatePipe],
})
export class DateTimeViewComponent {
  private readonly datePipe = inject(DatePipe);

  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type, e.g. `DateTime`. */
  @Input() type?: string;

  @Input() value: unknown = '';

  /** `undefined` when `fields` carries no known `InputMode`, so the template falls back to `shortDateTime`. */
  get formattedValue(): string | null | undefined {
    const mode = this.fields?.field.configuration['DateTime.InputMode'] as DateTimeInputMode;
    const format = DATE_INPUT_MODE_FORMATS[mode]?.format;
    return format ? this.datePipe.transform(this.value as string, format) : undefined;
  }
}
