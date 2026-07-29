import { Component } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { AbstractControl, ReactiveFormsModule } from '@angular/forms';
import { NzSelectModule } from 'ng-zorro-antd/select';
import { readStringList } from '../../utils';
import { FieldTypeControlBase } from '../field-type-control-base';
import { SelectConfiguration } from './select-configuration';
import { SelectListItem, normalizeSelectListItems } from './select-list-item';

/** Filters by a `Select` field. */
@Component({
  selector: 'ff-select-search',
  templateUrl: './select-search.component.html',
  imports: [CoreModule, ReactiveFormsModule, NzSelectModule],
})
export class SelectSearchComponent extends FieldTypeControlBase {
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
    // Unlike the control component, options marked `Selected` do not seed a filter: a default answer
    // is not a default *filter*, and pre-filtering a list nobody asked to filter hides rows.
    const stored = readStringList(this.selectedValue).filter(value => value !== '');

    return this.fb.control(this.multiple ? stored : (stored[0] ?? ''));
  }
}
