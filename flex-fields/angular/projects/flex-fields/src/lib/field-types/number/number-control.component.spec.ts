import { FormGroup, Validators } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '../../models';
import { NumberControlComponent } from './number-control.component';

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'price',
      displayName: 'Price',
      fieldTypeName: 'Number',
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
  const fixture = TestBed.createComponent(NumberControlComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  if (selected !== undefined) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { fixture, values };
}

function inputEvent(value: string): Event {
  return { target: { value } } as unknown as Event;
}

describe('NumberControlComponent', () => {
  it('creates a control under the field name in the values group', () => {
    const { values } = render(fieldValue());
    expect(values.contains('price')).toBe(true);
  });

  it('adds a required validator when the usage requires the field', () => {
    const { values } = render(fieldValue({ required: true }));
    expect(values.get('price')!.hasValidator(Validators.required)).toBe(true);
  });

  it('enforces the configured minimum', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'price', displayName: 'Price', fieldTypeName: 'Number',
        configuration: { 'Number.Min': 10 },
      },
    });
    const { values } = render(field, 5);
    expect(values.get('price')!.hasError('min')).toBe(true);
  });

  it('enforces the configured maximum', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'price', displayName: 'Price', fieldTypeName: 'Number',
        configuration: { 'Number.Max': 100 },
      },
    });
    const { values } = render(field, 500);
    expect(values.get('price')!.hasError('max')).toBe(true);
  });

  it('still enforces a configured minimum of exactly 0', () => {
    // createControl() itself gates on `if (min)`, under which a falsy 0 would look unconfigured - but
    // the template also binds the native `[min]` attribute, and Angular's directive-based min
    // validator picks that up independent of createControl()'s own (falsy-gated) push. Net effect: 0
    // is still enforced, just not by the mechanism you'd expect from reading createControl() alone.
    const field = fieldValue({
      field: {
        id: '1', name: 'price', displayName: 'Price', fieldTypeName: 'Number',
        configuration: { 'Number.Min': 0 },
      },
    });
    const { values } = render(field, -5);
    expect(values.get('price')!.hasError('min')).toBe(true);
  });

  it('truncates typed input to the configured decimal places', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'price', displayName: 'Price', fieldTypeName: 'Number',
        configuration: { 'Number.Decimals': 2 },
      },
    });
    const { fixture, values } = render(field);

    fixture.componentInstance.onInput(inputEvent('1.2345'));

    expect(values.get('price')!.value).toBe('1.23');
  });

  it('truncates to a whole number when the field allows no decimals', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'price', displayName: 'Price', fieldTypeName: 'Number',
        configuration: { 'Number.Decimals': 0 },
      },
    });
    const { fixture, values } = render(field);

    fixture.componentInstance.onInput(inputEvent('7.9'));

    expect(values.get('price')!.value).toBe('7');
  });

  it('does not touch the control when input is within the configured precision', () => {
    // The reactive-forms directive (not exercised here, since we call the handler directly) is what
    // normally syncs typed input into the control - onInput only intervenes to truncate.
    const field = fieldValue({
      field: {
        id: '1', name: 'price', displayName: 'Price', fieldTypeName: 'Number',
        configuration: { 'Number.Decimals': 2 },
      },
    });
    const { fixture, values } = render(field);
    const before = values.get('price')!.value;

    fixture.componentInstance.onInput(inputEvent('1.2'));

    expect(values.get('price')!.value).toBe(before);
  });
});
