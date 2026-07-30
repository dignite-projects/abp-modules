import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { AbstractControl, ReactiveFormsModule, ValidatorFn, Validators } from '@angular/forms';
import { readStringList } from '../../utils';
import { FieldTypeControlBase } from '../field-type-control-base';
import { SelectConfiguration } from './select-configuration';
import { SelectListItem, normalizeSelectListItems } from './select-list-item';

/** Edits the value of a `Select` field — a native select, or an ng-zorro multi-select. */
@Component({
  selector: 'ff-select-control',
  templateUrl: './select-control.component.html',
  imports: [CommonModule, ReactiveFormsModule],
})
export class SelectControlComponent extends FieldTypeControlBase {
  get multiple(): boolean {
    return !!this.fieldValue?.field.configuration['Select.Multiple'];
  }

  get nullText(): string {
    return (this.fieldValue?.field.configuration['Select.NullText'] as string) ?? '';
  }

  get options(): SelectListItem[] {
    return normalizeSelectListItems(this.fieldValue?.field.configuration['Select.Options']);
  }

  protected configurationDefaults(): object {
    return new SelectConfiguration();
  }

  protected createControl(): AbstractControl {
    const validators: ValidatorFn[] = [];

    if (this.fieldValue!.required) {
      validators.push(Validators.required);
    }

    return this.fb.control(this.resolveValue(), validators);
  }

  /**
   * A stored value wins; otherwise the options marked `Selected` in the configuration seed the field.
   *
   * The shape has to match the widget: an array for multi-select, a scalar for the native one. The
   * old library wrapped the single-select default in an array too, so a field with a default option
   * rendered as nothing selected.
   */
  private resolveValue(): unknown {
    const stored = readStringList(this.selectedValue).filter(value => value !== '');

    if (stored.length > 0) {
      return this.multiple ? stored : stored[0];
    }

    const defaults = this.options.filter(option => option.Selected).map(option => option.Value);

    if (this.multiple) {
      return defaults;
    }

    return defaults[0] ?? '';
  }
}
