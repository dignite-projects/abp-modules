import { Component, Input } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { FlexFieldValue } from '../../models';

/** Displays the value of a `Switch` field read-only. */
@Component({
  selector: 'ff-boolean-view',
  templateUrl: './boolean-view.component.html',
  imports: [CoreModule],
})
export class BooleanViewComponent {
  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type, e.g. `Switch`. */
  @Input() type?: string;

  @Input() value: unknown = '';
}
