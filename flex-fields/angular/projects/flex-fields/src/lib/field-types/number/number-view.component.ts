import { Component, Input } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { FlexFieldValue } from '../../models';

/** Displays the value of a `Number` field read-only. */
@Component({
  selector: 'ff-number-view',
  templateUrl: './number-view.component.html',
  imports: [CoreModule],
})
export class NumberViewComponent {
  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type, e.g. `Number`. */
  @Input() type?: string;

  @Input() value: unknown = '';
}
