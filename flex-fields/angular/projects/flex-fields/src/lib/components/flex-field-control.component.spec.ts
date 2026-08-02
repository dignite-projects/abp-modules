import { Component, Input } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { FieldTypeDefinition } from '../field-types';
import { FlexFieldValue } from '../models';
import { provideFlexFields, provideFlexFieldTypes } from '../providers';
import { FlexFieldControlComponent } from './flex-field-control.component';

@Component({ selector: 'ff-stub-control', template: '<span>stub</span>' })
class StubControlComponent {
  @Input() fields?: FlexFieldValue;
  @Input() parentFieldName?: string;
  @Input() selected: unknown;
  @Input() entity?: FormGroup;
}

@Component({
  template: `
    <form [formGroup]="form">
      @for (field of fields; track field.field.id) {
        <ff-flex-field-control
          [fields]="field"
          [entity]="form"
          parentFieldName="flexFields"
        />
      }
    </form>
  `,
  imports: [ReactiveFormsModule, FlexFieldControlComponent],
})
class BuiltInControlsHostComponent {
  readonly form = new FormGroup({ flexFields: new FormGroup({}) });
  fields: FlexFieldValue[] = [];
}

const stubType: FieldTypeDefinition = {
  name: 'Stub',
  displayNameKey: 'FlexFields::FieldType:Text',
  indexable: true,
  controlComponent: StubControlComponent,
};

function fieldValue(fieldTypeName: string): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'colour',
      displayName: 'Colour',
      fieldTypeName,
      configuration: {},
    },
    required: false,
    searchable: false,
  };
}

describe('FlexFieldControlComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideFlexFieldTypes(stubType)] });
  });

  function render(field: FlexFieldValue) {
    const fixture = TestBed.createComponent(FlexFieldControlComponent);
    fixture.componentRef.setInput('fields', field);
    fixture.componentRef.setInput('entity', new FormGroup({ flexFields: new FormGroup({}) }));
    fixture.componentRef.setInput('parentFieldName', 'flexFields');
    fixture.detectChanges();
    return fixture;
  }

  it('renders the control component registered for the field type', () => {
    const fixture = render(fieldValue('Stub'));

    expect(fixture.nativeElement.textContent).toContain('stub');
  });

  it('renders nothing for an unregistered field type instead of throwing', () => {
    // The host renders whatever the data says; a bolt-on the app forgot to register must not take
    // down the entire form it happens to appear on.
    const fixture = render(fieldValue('NotRegistered'));

    expect(fixture.nativeElement.textContent).not.toContain('stub');
  });

  it('does not recreate the dynamic control when its input references are unchanged', () => {
    const field = fieldValue('Stub');
    const entity = new FormGroup({ flexFields: new FormGroup({}) });
    const fixture = TestBed.createComponent(FlexFieldControlComponent);
    fixture.componentRef.setInput('fields', field);
    fixture.componentRef.setInput('entity', entity);
    fixture.componentRef.setInput('parentFieldName', 'flexFields');
    fixture.detectChanges();

    const first = fixture.componentInstance.fieldRef?.get(0);

    fixture.componentRef.setInput('fields', field);
    fixture.componentRef.setInput('entity', entity);
    fixture.componentRef.setInput('parentFieldName', 'flexFields');
    fixture.detectChanges();

    expect(fixture.componentInstance.fieldRef?.get(0)).toBe(first);
  });
});

describe('field names containing a dot', () => {
  it('resolves as one control, not as a path into a nested group', () => {
    // AbstractControl.get splits a string on '.', so get('price.net') looks for a group called
    // 'price'. Nothing prevents a field from being named that way, and the array form is what makes
    // the lookup literal.
    const values = new FormGroup({ 'price.net': new FormControl('9.99') });

    expect(values.get('price.net')).toBeNull();
    expect(values.get(['price.net'])?.value).toBe('9.99');
  });
});

describe('built-in field controls', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideFlexFields()],
    });
  });

  for (const fieldTypeName of [
    'TextEdit',
    'NumericEdit',
    'DateEdit',
    'Select',
    'Switch',
    'TreeView',
  ]) {
    it(`creates the ${fieldTypeName} control without a change-detection loop`, () => {
      const field = fieldValue(fieldTypeName);
      const values = new FormGroup({});
      const entity = new FormGroup({ flexFields: values });
      const fixture = TestBed.createComponent(FlexFieldControlComponent);
      fixture.componentRef.setInput('fields', field);
      fixture.componentRef.setInput('entity', entity);
      fixture.componentRef.setInput('parentFieldName', 'flexFields');

      fixture.detectChanges();
      fixture.detectChanges();

      expect(values.contains(field.field.name)).toBe(true);
      fixture.destroy();
    });
  }

  it('creates the complete product form in one change-detection pass', () => {
    const fixture = TestBed.createComponent(BuiltInControlsHostComponent);
    fixture.componentInstance.fields = [
      fieldValue('TextEdit'),
      fieldValue('NumericEdit'),
      fieldValue('DateEdit'),
      fieldValue('Select'),
      fieldValue('Switch'),
      fieldValue('TreeView'),
    ].map((field, index) => ({
      ...field,
      field: { ...field.field, id: `${index}`, name: `field${index}` },
    }));

    fixture.detectChanges();
    fixture.detectChanges();

    const values = fixture.componentInstance.form.controls.flexFields;
    expect(Object.keys(values.controls)).toHaveLength(6);
    expect(fixture.nativeElement.querySelectorAll('ff-flex-field-control')).toHaveLength(6);
    fixture.destroy();
  });
});
