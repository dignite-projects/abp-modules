import { Component } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { FieldTypeControlBase } from '../field-type-control-base';
import { NumericConfiguration } from './numeric-configuration';

/** Edits the value of a `NumericEdit` field. */
@Component({
  selector: 'ff-number-control',
  templateUrl: './number-control.component.html',
  imports: [CoreModule, ReactiveFormsModule],
})
export class NumberControlComponent extends FieldTypeControlBase {
  protected configurationDefaults(): object {
    return new NumericConfiguration();
  }

  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];
    const configuration = this.fieldValue!.field.configuration;

    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    const min = configuration['NumericEditField.Min'];
    if (min) {
      validators.push(Validators.min(Number(min)));
    }

    const max = configuration['NumericEditField.Max'];
    if (max) {
      validators.push(Validators.max(Number(max)));
    }

    return this.fb.control(this.selectedValue, validators);
  }

  get configuration(): Record<string, unknown> {
    return this.fieldValue?.field.configuration ?? {};
  }

  /**
   * Truncates typed input to the configured number of decimal places, rather than letting the value
   * through and failing validation afterwards.
   */
  onInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const value = input.value;
    const decimals = Number(this.configuration['NumericEditField.Decimals'] ?? 0);
    const decimalPart = value.split('.')[1];

    if (decimalPart && decimalPart.length > decimals) {
      const control = this.fieldControl;
      const [whole] = value.split('.');
      // The old library sliced by `decimalPart.length - 2`, hard-coding the default of 2 decimals, so
      // any field configured with a different precision was truncated to the wrong length.
      control?.patchValue(decimals > 0 ? `${whole}.${decimalPart.slice(0, decimals)}` : whole);
      control?.updateValueAndValidity();
    }
  }
}
