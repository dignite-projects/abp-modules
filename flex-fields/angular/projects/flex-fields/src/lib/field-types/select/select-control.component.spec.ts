import { FormGroup, Validators } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '../../models';
import { SelectControlComponent } from './select-control.component';

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
    searchable: false,
    ...overrides,
  };
}

function render(field: FlexFieldValue, selected?: unknown) {
  const values = new FormGroup({});
  const entity = new FormGroup({ flexFields: values });
  const fixture = TestBed.createComponent(SelectControlComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  if (selected !== undefined) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { fixture, values };
}

describe('SelectControlComponent', () => {
  it('renders a native select in single mode', () => {
    const { fixture } = render(fieldValue());
    expect(fixture.nativeElement.querySelector('select')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('nz-select')).toBeFalsy();
  });

  it('renders nz-select in multiple mode', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'color', displayName: 'Color', fieldTypeName: 'Select',
        configuration: { 'Select.Multiple': true, 'Select.Options': OPTIONS },
      },
    });
    const { fixture } = render(field);
    expect(fixture.nativeElement.querySelector('nz-select')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('select.form-select:not(nz-select select)')).toBeFalsy();
  });

  it('uses the stored value when one is selected', () => {
    const { values } = render(fieldValue(), 'red');
    expect(values.get('color')!.value).toBe('red');
  });

  it('falls back to the option marked Selected when nothing is stored', () => {
    const { values } = render(fieldValue());
    expect(values.get('color')!.value).toBe('blue');
  });

  it('returns an array in multiple mode, even for a single stored value', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'color', displayName: 'Color', fieldTypeName: 'Select',
        configuration: { 'Select.Multiple': true, 'Select.Options': OPTIONS },
      },
    });
    const { values } = render(field, 'red');
    expect(values.get('color')!.value).toEqual(['red']);
  });

  it('collects every option marked Selected as the multiple-mode default', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'color', displayName: 'Color', fieldTypeName: 'Select',
        configuration: {
          'Select.Multiple': true,
          'Select.Options': [
            { Text: 'Red', Value: 'red', Selected: true },
            { Text: 'Blue', Value: 'blue', Selected: true },
            { Text: 'Green', Value: 'green', Selected: false },
          ],
        },
      },
    });
    const { values } = render(field);
    expect(values.get('color')!.value).toEqual(['red', 'blue']);
  });

  it('resolves to an empty string in single mode with no stored or default value', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'color', displayName: 'Color', fieldTypeName: 'Select',
        configuration: { 'Select.Multiple': false, 'Select.Options': [] },
      },
    });
    const { values } = render(field);
    expect(values.get('color')!.value).toBe('');
  });

  it('normalizes camelCase options from the server the same as PascalCase', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'color', displayName: 'Color', fieldTypeName: 'Select',
        configuration: { 'Select.Multiple': false, 'Select.Options': [{ text: 'Red', value: 'red', selected: true }] },
      },
    });
    const { values } = render(field);
    expect(values.get('color')!.value).toBe('red');
  });

  it('adds a required validator when the usage requires the field', () => {
    const { values } = render(fieldValue({ required: true }));
    expect(values.get('color')!.hasValidator(Validators.required)).toBe(true);
  });
});
