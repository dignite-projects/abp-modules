import { Component, inject } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { DatePipe } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { FieldTypeConfigBase } from '../field-type-config-base';
import { DATE_INPUT_MODE_FORMATS, DateTimeConfiguration } from './date-time-configuration';
import { DateTimeInputMode } from './date-time-input-mode';

let nextRadioId = 0;

/** Designer-side editor for a `DateTime` field's configuration. */
@Component({
  selector: 'ff-date-time-config',
  templateUrl: './date-time-config.component.html',
  imports: [CoreModule, ReactiveFormsModule],
  // DatePipe is not `providedIn: 'root'`. The old library injected it and relied on the host app
  // happening to provide it, which failed with a null-injector error in any host that did not.
  providers: [DatePipe],
})
export class DateTimeConfigComponent extends FieldTypeConfigBase {
  private readonly datePipe = inject(DatePipe);

  readonly DateTimeInputMode = DateTimeInputMode;

  readonly radioIds: Record<DateTimeInputMode, string> = {
    [DateTimeInputMode.Date]: `ff-date-mode-date-${nextRadioId}`,
    [DateTimeInputMode.DateTime]: `ff-date-mode-datetime-${nextRadioId}`,
    [DateTimeInputMode.Month]: `ff-date-mode-month-${nextRadioId++}`,
  };

  /** Which `<input type>` the Min/Max boxes use — it tracks the selected input mode. */
  dateTimeType = DATE_INPUT_MODE_FORMATS[DateTimeInputMode.Date].inputType;

  protected configurationDefaults(): object {
    return new DateTimeConfiguration();
  }

  protected override onConfigurationPatched(): void {
    this.onInputModeChange();
  }

  /**
   * Re-formats the Min/Max bounds to the newly selected mode. `<input type="month">` will not accept
   * a full ISO timestamp, so switching mode without reformatting silently blanks both bounds.
   */
  onInputModeChange(): void {
    const mode = this.configuration.value['DateTime.InputMode'] as DateTimeInputMode;
    const { inputType, format } = DATE_INPUT_MODE_FORMATS[mode] ?? DATE_INPUT_MODE_FORMATS[DateTimeInputMode.Date];

    this.dateTimeType = inputType;
    this.configuration.patchValue({
      'DateTime.Min': this.datePipe.transform(this.configuration.value['DateTime.Min'], format),
      'DateTime.Max': this.datePipe.transform(this.configuration.value['DateTime.Max'], format),
    });
  }
}
