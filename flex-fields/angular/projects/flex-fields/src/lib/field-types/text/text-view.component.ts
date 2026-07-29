import { Component, Input } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { FlexFieldValue } from '../../models';

/** Displays the value of a `TextEdit` field read-only. */
@Component({
  selector: 'ff-text-view',
  templateUrl: './text-view.component.html',
  imports: [CoreModule],
})
export class TextViewComponent {
  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type, e.g. `TextEdit`. */
  @Input() type?: string;

  @Input() value: unknown = '';
}
