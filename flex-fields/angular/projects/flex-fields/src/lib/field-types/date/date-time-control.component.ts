import { Component, inject } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { FieldTypeControlBase } from '../field-type-control-base';
import { DATE_INPUT_MODE_FORMATS, DateConfiguration } from './date-configuration';
import { DateInputMode } from './date-input-mode';

/** Edits the value of a `DateEdit` field. */
@Component({
  selector: 'ff-date-time-control',
  templateUrl: './date-time-control.component.html',
  imports: [CommonModule, ReactiveFormsModule],
  providers: [DatePipe],
})
export class DateTimeControlComponent extends FieldTypeControlBase {
  private readonly datePipe = inject(DatePipe);

  readonly DateInputMode = DateInputMode;

  get configuration(): Record<string, unknown> {
    return this.fieldValue?.field.configuration ?? {};
  }

  protected configurationDefaults(): object {
    return new DateConfiguration();
  }

  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];

    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    // No Validators.min/max here: they coerce with Number(), and Number('2020-01-01') is NaN, so on
    // the old library they silently never fired. The bounds are enforced by the native min/max
    // attributes in the template, which is what was actually doing the work all along.

    const mode = this.configuration['DateEdit.InputMode'] as DateInputMode;
    const format = DATE_INPUT_MODE_FORMATS[mode]?.format;
    // The `<input type="date|datetime-local|month">` elements only accept their own literal format;
    // handing them a raw ISO timestamp leaves the box blank.
    const value = format ? this.datePipe.transform(this.selectedValue as string, format) : this.selectedValue;

    return this.fb.control(value, validators);
  }
}
