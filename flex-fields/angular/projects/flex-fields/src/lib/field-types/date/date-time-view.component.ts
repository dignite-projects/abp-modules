import { Component, Input } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { FlexFieldValue } from '../../models';

/** Displays the value of a `DateEdit` field read-only, in the user's short date-time format. */
@Component({
  selector: 'ff-date-time-view',
  templateUrl: './date-time-view.component.html',
  imports: [CoreModule],
})
export class DateTimeViewComponent {
  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type, e.g. `DateEdit`. */
  @Input() type?: string;

  @Input() value: unknown = '';
}
