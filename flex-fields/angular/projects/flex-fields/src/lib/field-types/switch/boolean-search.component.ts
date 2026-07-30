import { Component } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { CommonModule } from '@angular/common';
import { AbstractControl, ReactiveFormsModule } from '@angular/forms';
import { FieldTypeControlBase } from '../field-type-control-base';
import { SwitchConfiguration } from './switch-configuration';

/**
 * Filters by a `Switch` field. A tri-state select rather than a checkbox — "either" has to be
 * expressible, and an unchecked checkbox would read as "false" instead.
 */
@Component({
  selector: 'ff-boolean-search',
  templateUrl: './boolean-search.component.html',
  imports: [CommonModule, ReactiveFormsModule, LocalizationPipe],
})
export class BooleanSearchComponent extends FieldTypeControlBase {
  protected configurationDefaults(): object {
    return new SwitchConfiguration();
  }

  protected createControl(): AbstractControl {
    // Unlike the control component, no fallback to `Switch.Default`: a filter starts unset.
    return this.fb.control(this.selectedValue);
  }
}
