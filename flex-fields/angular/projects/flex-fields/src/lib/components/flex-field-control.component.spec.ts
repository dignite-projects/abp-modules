import { Component, Input } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { FieldTypeDefinition } from '../field-types';
import { FlexFieldValue } from '../models';
import { provideFlexFieldTypes } from '../providers';
import { FlexFieldControlComponent } from './flex-field-control.component';

@Component({ selector: 'ff-stub-control', template: '<span>stub</span>' })
class StubControlComponent {
  @Input() fields?: FlexFieldValue;
  @Input() parentFieldName?: string;
  @Input() selected: unknown;
  @Input() entity?: FormGroup;
}

const stubType: FieldTypeDefinition = {
  name: 'Stub',
  displayNameKey: 'FlexFields::FieldType:Text',
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
