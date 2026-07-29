import { Component, Input } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { FlexFieldValue } from '../../models';
import { readStringList } from '../../utils';
import { normalizeSelectListItems } from './select-list-item';

/**
 * Displays the value of a `Select` field read-only, showing each option's label rather than the
 * stored value.
 */
@Component({
  selector: 'ff-select-view',
  templateUrl: './select-view.component.html',
  imports: [CoreModule],
})
export class SelectViewComponent {
  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type, e.g. `Select`. */
  @Input() type?: string;

  @Input() value: unknown = '';

  get displayValue(): string {
    const options = normalizeSelectListItems(this.fields?.field.configuration['Select.Options']);

    return readStringList(this.value)
      .map(stored => options.find(option => option.Value === stored)?.Text ?? stored)
      .join(', ');
  }
}
