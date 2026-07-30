import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NzSelectModule, NzSelectOptionInterface } from 'ng-zorro-antd/select';
import { AbstractControl, ReactiveFormsModule } from '@angular/forms';
import { readStringList } from '../../utils';
import { FieldTypeControlBase } from '../field-type-control-base';
import { SelectConfiguration } from './select-configuration';
import { SelectListItem, normalizeSelectListItems } from './select-list-item';

/** Filters by a `Select` field. */
@Component({
  selector: 'ff-select-search',
  templateUrl: './select-search.component.html',
  styleUrls: ['./select-field.component.scss'],
  imports: [CommonModule, ReactiveFormsModule, NzSelectModule],
})
export class SelectSearchComponent extends FieldTypeControlBase {
  private optionsSource: unknown;
  private normalizedOptions: SelectListItem[] = [];
  private selectOptions: NzSelectOptionInterface[] = [];

  get multiple(): boolean {
    return !!this.fieldValue?.field.configuration['Select.Multiple'];
  }

  get nullText(): string {
    return (this.fieldValue?.field.configuration['Select.NullText'] as string) ?? '';
  }

  get options(): SelectListItem[] {
    const source = this.fieldValue?.field.configuration['Select.Options'];

    if (source !== this.optionsSource) {
      this.optionsSource = source;
      this.normalizedOptions = normalizeSelectListItems(source);
      this.selectOptions = this.normalizedOptions.map(item => ({
        label: item.Text,
        value: item.Value,
      }));
    }

    return this.normalizedOptions;
  }

  get nzOptions(): NzSelectOptionInterface[] {
    this.options;
    return this.selectOptions;
  }

  protected configurationDefaults(): object {
    return new SelectConfiguration();
  }

  protected createControl(): AbstractControl {
    // Unlike the control component, options marked `Selected` do not seed a filter: a default answer
    // is not a default *filter*, and pre-filtering a list nobody asked to filter hides rows.
    const stored = readStringList(this.selectedValue).filter(value => value !== '');

    return this.fb.control(this.multiple ? stored : (stored[0] ?? ''));
  }
}
