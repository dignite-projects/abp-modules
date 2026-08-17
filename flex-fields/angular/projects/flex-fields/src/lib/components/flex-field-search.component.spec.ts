import { Component, Input } from '@angular/core';
import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { FieldTypeDefinition } from '../field-types';
import { FlexFieldValue } from '../models';
import { provideFlexFields, provideFlexFieldTypes } from '../providers';
import { FlexFieldSearchComponent } from './flex-field-search.component';

@Component({ selector: 'ff-stub-search', template: '<span>stub</span>' })
class StubSearchComponent {
  @Input() fields?: FlexFieldValue;
  @Input() parentFieldName?: string;
  @Input() selected: unknown;
  @Input() entity?: FormGroup;
}

const stubType: FieldTypeDefinition = {
  name: 'Stub',
  displayNameKey: 'FlexFields::FieldType:Text',
  searchComponent: StubSearchComponent,
};

function fieldValue(fieldTypeName: string): FlexFieldValue {
  return {
    field: { id: '1', name: 'colour', displayName: 'Colour', fieldTypeName, configuration: {} },
    required: false,
    searchable: true,
  };
}

describe('FlexFieldSearchComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideFlexFieldTypes(stubType)] });
  });

  function render(field: FlexFieldValue) {
    const fixture = TestBed.createComponent(FlexFieldSearchComponent);
    fixture.componentRef.setInput('fields', field);
    fixture.componentRef.setInput('entity', new FormGroup({ flexFields: new FormGroup({}) }));
    fixture.componentRef.setInput('parentFieldName', 'flexFields');
    fixture.detectChanges();
    return fixture;
  }

  it('renders the search widget registered for the field type', () => {
    const fixture = render(fieldValue('Stub'));

    expect(fixture.nativeElement.textContent).toContain('stub');
  });

  it('renders nothing for an unregistered field type instead of throwing', () => {
    const fixture = render(fieldValue('NotRegistered'));

    expect(fixture.nativeElement.textContent).not.toContain('stub');
  });

  it('does not recreate the dynamic widget when its input references are unchanged', () => {
    const field = fieldValue('Stub');
    const entity = new FormGroup({ flexFields: new FormGroup({}) });
    const fixture = TestBed.createComponent(FlexFieldSearchComponent);
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

describe('built-in field search widgets', () => {
  beforeEach(() => {
    // BooleanSearchComponent's Yes/No options go through abpLocalization.
    TestBed.configureTestingModule({
      providers: [provideFlexFields()],
      imports: [CoreTestingModule.withConfig()],
    });
  });

  // DateTime deliberately excluded - see the next test.
  for (const fieldTypeName of ['Text', 'Number', 'Select', 'Boolean', 'Tree']) {
    it(`creates the ${fieldTypeName} search widget without a change-detection loop`, () => {
      const field = fieldValue(fieldTypeName);
      const values = new FormGroup({});
      const entity = new FormGroup({ flexFields: values });
      const fixture = TestBed.createComponent(FlexFieldSearchComponent);
      fixture.componentRef.setInput('fields', field);
      fixture.componentRef.setInput('entity', entity);
      fixture.componentRef.setInput('parentFieldName', 'flexFields');

      fixture.detectChanges();
      fixture.detectChanges();

      expect(values.contains(field.field.name)).toBe(true);
      fixture.destroy();
    });
  }

  it('renders nothing for DateTime - the one built-in type with no search widget', () => {
    const field = fieldValue('DateTime');
    const entity = new FormGroup({ flexFields: new FormGroup({}) });
    const fixture = TestBed.createComponent(FlexFieldSearchComponent);
    fixture.componentRef.setInput('fields', field);
    fixture.componentRef.setInput('entity', entity);
    fixture.componentRef.setInput('parentFieldName', 'flexFields');

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelectorAll('*').length).toBe(0);
  });
});
