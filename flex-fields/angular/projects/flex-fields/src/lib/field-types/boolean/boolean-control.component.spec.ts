import { FormGroup, Validators } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '../../models';
import { BooleanControlComponent } from './boolean-control.component';

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'isActive',
      displayName: 'Active',
      fieldTypeName: 'Boolean',
      configuration: {},
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

function render(field: FlexFieldValue, selected?: unknown) {
  const values = new FormGroup({});
  const entity = new FormGroup({ flexFields: values });
  const fixture = TestBed.createComponent(BooleanControlComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  if (selected !== undefined) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { fixture, values };
}

describe('BooleanControlComponent', () => {
  it('creates a control under the field name in the values group', () => {
    const { values } = render(fieldValue());
    expect(values.contains('isActive')).toBe(true);
  });

  it('seeds the control with the configured default when nothing is selected', () => {
    const field = fieldValue({
      field: {
        id: '1',
        name: 'isActive',
        displayName: 'Active',
        fieldTypeName: 'Boolean',
        configuration: { 'Boolean.Default': true },
      },
    });

    expect(render(field).values.get('isActive')!.value).toBe(true);
  });

  it('seeds an explicit false selection instead of falling back to the default', () => {
    // A stored `false` is a real answer, not "nothing selected" - it must survive even when the
    // configured default is true.
    const field = fieldValue({
      field: {
        id: '1',
        name: 'isActive',
        displayName: 'Active',
        fieldTypeName: 'Boolean',
        configuration: { 'Boolean.Default': true },
      },
    });

    expect(render(field, false).values.get('isActive')!.value).toBe(false);
  });

  it('seeds the control with a truthy selection', () => {
    expect(render(fieldValue(), true).values.get('isActive')!.value).toBe(true);
  });

  it('adds a required validator when the usage requires the field', () => {
    const { values } = render(fieldValue({ required: true }));
    expect(values.get('isActive')!.hasValidator(Validators.required)).toBe(true);
  });

  it('adds no validator when the usage does not require the field', () => {
    const { values } = render(fieldValue({ required: false }));
    expect(values.get('isActive')!.hasValidator(Validators.required)).toBe(false);
  });

  it('removes the control from the values group on destroy', () => {
    const { fixture, values } = render(fieldValue());
    expect(values.contains('isActive')).toBe(true);

    fixture.destroy();

    expect(values.contains('isActive')).toBe(false);
  });

  it('fills in the default configuration key when the stored field predates it', () => {
    const field = fieldValue({
      field: {
        id: '1',
        name: 'isActive',
        displayName: 'Active',
        fieldTypeName: 'Boolean',
        configuration: {},
      },
    });

    render(field);

    expect(field.field.configuration['Boolean.Default']).toBe(false);
  });
});
