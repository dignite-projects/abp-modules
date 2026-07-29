import { Component, inject } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { DatePipe } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { FieldTypeConfigBase } from '../field-type-config-base';
import { DATE_INPUT_MODE_FORMATS, DateConfiguration } from './date-configuration';
import { DateInputMode } from './date-input-mode';

let nextRadioId = 0;

/** Designer-side editor for a `DateEdit` field's configuration. */
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

  readonly DateInputMode = DateInputMode;

  readonly radioIds: Record<DateInputMode, string> = {
    [DateInputMode.Date]: `ff-date-mode-date-${nextRadioId}`,
    [DateInputMode.DateTime]: `ff-date-mode-datetime-${nextRadioId}`,
    [DateInputMode.Month]: `ff-date-mode-month-${nextRadioId++}`,
  };

  /** Which `<input type>` the Min/Max boxes use — it tracks the selected input mode. */
  dateTimeType = DATE_INPUT_MODE_FORMATS[DateInputMode.Date].inputType;

  protected configurationDefaults(): object {
    return new DateConfiguration();
  }

  protected override onConfigurationPatched(): void {
    this.onInputModeChange();
  }

  /**
   * Re-formats the Min/Max bounds to the newly selected mode. `<input type="month">` will not accept
   * a full ISO timestamp, so switching mode without reformatting silently blanks both bounds.
   */
  onInputModeChange(): void {
    const mode = this.configuration.value['DateEdit.InputMode'] as DateInputMode;
    const { inputType, format } = DATE_INPUT_MODE_FORMATS[mode] ?? DATE_INPUT_MODE_FORMATS[DateInputMode.Date];

    this.dateTimeType = inputType;
    this.configuration.patchValue({
      'DateEdit.Min': this.datePipe.transform(this.configuration.value['DateEdit.Min'], format),
      'DateEdit.Max': this.datePipe.transform(this.configuration.value['DateEdit.Max'], format),
    });
  }
}
