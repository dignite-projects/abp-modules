import { Component, Input, OnChanges, Type, ViewChild, ViewContainerRef, inject } from '@angular/core';
import { FieldTypeResolver } from '../field-types';
import { FlexFieldValue } from '../models';
import { setDynamicInputs } from '../utils';

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

  private renderedType?: Type<unknown>;
  private renderedFields?: FlexFieldValue;
  private renderedValue: unknown;
  private renderedShowInList = false;

  ngOnChanges(): void {
    const fieldType = this.type ? this.fieldTypes.find(this.type) : undefined;
    const componentType = fieldType?.viewComponent;

    if (!componentType) {
      this.fieldRef?.clear();
      this.renderedType = undefined;
      this.renderedFields = undefined;
      this.renderedValue = undefined;
      this.renderedShowInList = false;
      return;
    }

    if (
      this.renderedType === componentType &&
      this.renderedFields === this.fields &&
      this.renderedValue === this.value &&
      this.renderedShowInList === this.showInList
    ) {
      return;
    }

    this.render(componentType);
  }

  private render(componentType: Type<unknown>): void {
    this.fieldRef?.clear();

    // An empty value still renders: a view component decides for itself how to show "nothing", and a
    // Switch showing "No" is not the same as a Switch showing nothing at all.
    const componentRef = this.fieldRef!.createComponent(componentType);

    setDynamicInputs(componentRef, {
      type: this.type,
      value: this.value,
      fields: this.fields,
      showInList: this.showInList,
    });

    this.renderedType = componentType;
    this.renderedFields = this.fields;
    this.renderedValue = this.value;
    this.renderedShowInList = this.showInList;
  }
}
