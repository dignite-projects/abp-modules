import { Component, Input, OnChanges, ViewChild, ViewContainerRef, inject } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { FieldTypeResolver } from '../field-types';
import { FlexFieldValue } from '../models';

/**
 * Renders the **edit** control for one flex field, whichever type it is.
 *
 * This is the seam the whole library exists for: the host knows it has a field, not which kind, so
 * it renders this and the registered field type decides what appears.
 */
@Component({
  selector: 'ff-flex-field-control',
  templateUrl: './flex-field-control.component.html',
})
export class FlexFieldControlComponent implements OnChanges {
  private readonly fieldTypes = inject(FieldTypeResolver);

  /** Definition + per-usage flags + current value for the field to render. */
  @Input() fields?: FlexFieldValue;

  /** The host form. */
  @Input() entity?: FormGroup;

  /** Name of the group inside the host form that holds flex field values. */
  @Input() parentFieldName?: string;

  /** The value to seed the control with. */
  @Input() selected: unknown = '';

  @ViewChild('fieldRef', { read: ViewContainerRef, static: true })
  fieldRef?: ViewContainerRef;

  ngOnChanges(): void {
    this.render();
  }

  private render(): void {
    this.fieldRef?.clear();

    if (!this.fields || !this.entity || !this.parentFieldName) {
      return;
    }

    const fieldType = this.fieldTypes.find(this.fields.field.fieldTypeName);

    if (!fieldType?.controlComponent) {
      return;
    }

    const componentRef = this.fieldRef!.createComponent(fieldType.controlComponent);

    // setInput rather than assigning to the instance: it marks the created component for check, which
    // assignment does not, so a field rendered inside an OnPush host would otherwise never update.
    componentRef.setInput('fields', this.fields);
    componentRef.setInput('parentFieldName', this.parentFieldName);
    componentRef.setInput('selected', this.selected);
    componentRef.setInput('entity', this.entity);
  }
}
