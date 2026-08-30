import { Component, Input, OnChanges, Type, ViewChild, ViewContainerRef, inject } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { FieldTypeResolver } from '../field-types';
import { FlexFieldValue } from '../models';
import { setDynamicInputs } from '../utils';

/**
 * Renders the **filter** widget for one flex field, whichever type it is.
 *
 * Renders nothing when the field's type has no search component — `DateTime`, today. That is the only
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

  private renderedType?: Type<unknown>;
  private renderedFields?: FlexFieldValue;
  private renderedEntity?: FormGroup;
  private renderedParentFieldName?: string;
  private renderedSelected: unknown;

  ngOnChanges(): void {
    if (!this.fields || !this.entity || !this.parentFieldName) {
      this.fieldRef?.clear();
      this.resetRenderedState();
      return;
    }

    const fieldType = this.fieldTypes.find(this.fields.field.fieldTypeName);

    if (!fieldType?.searchComponent) {
      this.fieldRef?.clear();
      this.resetRenderedState();
      return;
    }

    if (
      this.renderedType === fieldType.searchComponent &&
      this.renderedFields === this.fields &&
      this.renderedEntity === this.entity &&
      this.renderedParentFieldName === this.parentFieldName &&
      this.renderedSelected === this.selected
    ) {
      return;
    }

    this.render(fieldType.searchComponent);
  }

  private render(componentType: Type<unknown>): void {
    this.fieldRef?.clear();

    const componentRef = this.fieldRef!.createComponent(componentType);

    setDynamicInputs(componentRef, {
      fields: this.fields,
      parentFieldName: this.parentFieldName,
      selected: this.selected,
      entity: this.entity,
    });

    this.renderedType = componentType;
    this.renderedFields = this.fields;
    this.renderedEntity = this.entity;
    this.renderedParentFieldName = this.parentFieldName;
    this.renderedSelected = this.selected;
  }

  private resetRenderedState(): void {
    this.renderedType = undefined;
    this.renderedFields = undefined;
    this.renderedEntity = undefined;
    this.renderedParentFieldName = undefined;
    this.renderedSelected = undefined;
  }
}
