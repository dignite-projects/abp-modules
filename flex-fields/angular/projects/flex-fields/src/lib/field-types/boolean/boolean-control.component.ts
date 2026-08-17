import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { FieldTypeControlBase } from '../field-type-control-base';
import { BooleanConfiguration } from './boolean-configuration';

/** Edits the value of a `Switch` field. */
@Component({
  selector: 'ff-boolean-control',
  templateUrl: './boolean-control.component.html',
  imports: [CommonModule, ReactiveFormsModule],
})
export class BooleanControlComponent extends FieldTypeControlBase {
  protected configurationDefaults(): object {
    return new BooleanConfiguration();
  }

  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];

    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    // A stored `false` is a real answer, so it has to survive the falsy check that otherwise falls
    // back to the configured default.
    const stored = this.selectedValue;
    const value =
      stored === false || stored
        ? stored
        : this.fieldValue!.field.configuration['Boolean.Default'];

    return this.fb.control(value, validators);
  }
}
