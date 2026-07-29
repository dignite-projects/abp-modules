import { Component, Input, OnChanges, ViewChild, ViewContainerRef, inject } from '@angular/core';
import { FieldTypeResolver } from '../field-types';
import { FlexFieldValue } from '../models';

/** Renders the read-only **display** of one flex field's value, whichever type it is. */
@Component({
  selector: 'ff-flex-field-view',
  templateUrl: './flex-field-view.component.html',
})
export class FlexFieldViewComponent implements OnChanges {
  private readonly fieldTypes = inject(FieldTypeResolver);

  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type to render with. */
  @Input() type?: string;

  @Input() value: unknown = '';

  @ViewChild('fieldRef', { read: ViewContainerRef, static: true })
  fieldRef?: ViewContainerRef;

  ngOnChanges(): void {
    this.render();
  }

  private render(): void {
    this.fieldRef?.clear();

    // An empty value still renders: a view component decides for itself how to show "nothing", and a
    // Switch showing "No" is not the same as a Switch showing nothing at all.
    if (!this.type) {
      return;
    }

    const fieldType = this.fieldTypes.find(this.type);

    if (!fieldType?.viewComponent) {
      return;
    }

    const componentRef = this.fieldRef!.createComponent(fieldType.viewComponent);

    componentRef.setInput('type', this.type);
    componentRef.setInput('value', this.value);
    componentRef.setInput('fields', this.fields);
    componentRef.setInput('showInList', this.showInList);
  }
}
