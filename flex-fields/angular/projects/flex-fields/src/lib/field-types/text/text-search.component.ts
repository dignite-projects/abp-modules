import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, ReactiveFormsModule } from '@angular/forms';
import { FieldTypeControlBase } from '../field-type-control-base';
import { TextConfiguration } from './text-configuration';

/** Filters by a `Text` field. Never required and never length-limited — it is a filter, not a value. */
@Component({
  selector: 'ff-text-search',
  templateUrl: './text-search.component.html',
  imports: [CommonModule, ReactiveFormsModule],
})
export class TextSearchComponent extends FieldTypeControlBase {
  protected configurationDefaults(): object {
    return new TextConfiguration();
  }

  protected createControl(): AbstractControl {
    return this.fb.control(this.selectedValue);
  }
}
