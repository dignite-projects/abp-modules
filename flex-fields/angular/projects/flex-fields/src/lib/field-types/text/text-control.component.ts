import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { FieldTypeControlBase } from '../field-type-control-base';
import { TextConfiguration } from './text-configuration';
import { TextMode } from './text-mode';

/** Edits the value of a `TextEdit` field. */
@Component({
  selector: 'ff-text-control',
  templateUrl: './text-control.component.html',
  imports: [CommonModule, ReactiveFormsModule],
})
export class TextControlComponent extends FieldTypeControlBase {
  readonly TextMode = TextMode;

  protected configurationDefaults(): object {
    return new TextConfiguration();
  }

  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];

    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    const charLimit = this.fieldValue!.field.configuration['TextEdit.CharLimit'];
    if (charLimit) {
      validators.push(Validators.maxLength(Number(charLimit)));
    }

    return this.fb.control(this.selectedValue, validators);
  }
}
