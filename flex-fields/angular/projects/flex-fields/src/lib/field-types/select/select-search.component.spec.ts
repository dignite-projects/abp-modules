import { FormGroup, Validators } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '../../models';
import { SelectSearchComponent } from './select-search.component';

const OPTIONS = [
  { Text: 'Red', Value: 'red', Selected: false },
  { Text: 'Blue', Value: 'blue', Selected: true },
];

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'color',
      displayName: 'Color',
      fieldTypeName: 'Select',
      configuration: { 'Select.Multiple': false, 'Select.Options': OPTIONS },
    },
    required: false,
    searchable: true,
    ...overrides,
  };
}

function render(field: FlexFieldValue, selected?: unknown) {
  const values = new FormGroup({});
  const entity = new FormGroup({ flexFields: values });
  const fixture = TestBed.createComponent(SelectSearchComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  if (selected !== undefined) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { values };
}

describe('SelectSearchComponent', () => {
  it('uses the stored value in single mode', () => {
    const { values } = render(fieldValue(), 'red');
    expect(values.get('color')!.value).toBe('red');
  });

  it('does not fall back to the option marked Selected - a default answer is not a default filter', () => {
    const { values } = render(fieldValue());
    expect(values.get('color')!.value).toBe('');
  });

  it('uses the stored array in multiple mode', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'color', displayName: 'Color', fieldTypeName: 'Select',
        configuration: { 'Select.Multiple': true, 'Select.Options': OPTIONS },
      },
    });
    const { values } = render(field, ['red', 'blue']);
    expect(values.get('color')!.value).toEqual(['red', 'blue']);
  });

  it('starts empty in multiple mode rather than pre-filtering by the Selected defaults', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'color', displayName: 'Color', fieldTypeName: 'Select',
        configuration: { 'Select.Multiple': true, 'Select.Options': OPTIONS },
      },
    });
    const { values } = render(field);
    expect(values.get('color')!.value).toEqual([]);
  });

  it('adds no required validator even when the field usage requires it', () => {
    const { values } = render(fieldValue({ required: true }));
    expect(values.get('color')!.hasValidator(Validators.required)).toBe(false);
  });
});
