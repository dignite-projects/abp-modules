import { Component, Input, OnChanges, ViewChild, ViewContainerRef, inject } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { FieldTypeResolver } from '../field-types';
import { FlexFieldValue } from '../models';

/**
 * Renders the **filter** widget for one flex field, whichever type it is.
 *
 * Renders nothing when the field's type has no search component — `DateEdit`, today. That is the only
 * condition under which it should stay empty; the old library also required an unused `culture` input
 * before it would render anything, which made a missing filter look like a missing field type.
 */
@Component({
  selector: 'ff-flex-field-search',
  templateUrl: './flex-field-search.component.html',
})
export class FlexFieldSearchComponent implements OnChanges {
  private readonly fieldTypes = inject(FieldTypeResolver);

  @Input() fields?: FlexFieldValue;

  /** The filter form. */
  @Input() entity?: FormGroup;

  /** Name of the group inside the filter form that holds flex field conditions. */
  @Input() parentFieldName?: string;

  /** The filter value to seed the widget with. */
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

    if (!fieldType?.searchComponent) {
      return;
    }

    const componentRef = this.fieldRef!.createComponent(fieldType.searchComponent);

    componentRef.setInput('fields', this.fields);
    componentRef.setInput('parentFieldName', this.parentFieldName);
    componentRef.setInput('selected', this.selected);
    componentRef.setInput('entity', this.entity);
  }
}
