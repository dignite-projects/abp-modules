import {
  Component,
  Input,
  OnChanges,
  OnDestroy,
  ViewChild,
  ViewContainerRef,
  inject,
} from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { Subscription } from 'rxjs';
import { FieldTypeResolver } from '../field-types';
import { FlexFieldData } from '../models';

/**
 * Renders the designer-side **configuration** editor for whichever field type is currently selected,
 * and swaps it when the selection changes.
 */
@Component({
  selector: 'ff-flex-field-config',
  templateUrl: './flex-field-config.component.html',
})
export class FlexFieldConfigComponent implements OnChanges, OnDestroy {
  private readonly fieldTypes = inject(FieldTypeResolver);
  private fieldTypeChanges?: Subscription;

  /** Registration key of the field type to configure. */
  @Input() type?: string;

  /** The field definition being edited, if an existing one was selected. */
  @Input() selected?: FlexFieldData;

  /** The field-definition form. */
  @Input() form?: FormGroup;

  @ViewChild('fieldRef', { read: ViewContainerRef, static: true })
  fieldRef?: ViewContainerRef;

  /**
   * The control on the field-definition form that holds which type the field is.
   *
   * Named `fieldTypeName` to match the server's `IFlexFieldData.FieldTypeName`. The old library called
   * it `formControlName`, which collided head-on with Angular's own directive of that name.
   */
  private get fieldTypeControl(): FormControl | null {
    return (this.form?.get('fieldTypeName') as FormControl) ?? null;
  }

  ngOnChanges(): void {
    this.render(this.type);

    // Re-render when the designer switches the field to a different type.
    this.fieldTypeChanges?.unsubscribe();
    this.fieldTypeChanges = this.fieldTypeControl?.valueChanges.subscribe(fieldTypeName =>
      this.render(fieldTypeName),
    );
  }

  ngOnDestroy(): void {
    this.fieldTypeChanges?.unsubscribe();
  }

  private render(fieldTypeName?: string): void {
    this.fieldRef?.clear();

    if (!fieldTypeName || !this.form) {
      return;
    }

    const fieldType = this.fieldTypes.find(fieldTypeName);

    if (!fieldType?.configComponent) {
      return;
    }

    const componentRef = this.fieldRef!.createComponent(fieldType.configComponent);

    // A copy, not the live object: the editor patches stored values into its own form group, and
    // handing it the original would let an abandoned edit mutate the field the caller still holds.
    componentRef.setInput(
      'selected',
      this.selected ? (structuredClone(this.selected) as FlexFieldData) : this.selected,
    );
    componentRef.setInput('type', fieldTypeName);
    componentRef.setInput('Entity', this.form);
  }
}
