import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '../../models';
import { NumberSearchComponent } from './number-search.component';

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
    searchable: true,
    ...overrides,
  };
}

function render(field: FlexFieldValue) {
  const values = new FormGroup({});
  const entity = new FormGroup({ flexFields: values });
  const fixture = TestBed.createComponent(NumberSearchComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  fixture.detectChanges();
  return { fixture, values, component: fixture.componentInstance };
}

describe('NumberSearchComponent', () => {
  it('writes nothing to the field control until both ends of the range are set', () => {
    const { values, component } = render(fieldValue());

    component.numberForm.patchValue({ min: 5 });
    expect(values.get('price')!.value).toBe('');

    component.numberForm.patchValue({ max: 20 });
    expect(values.get('price')!.value).toBe('5-20');
  });

  it('passes typed values through unclamped when no bounds are configured', () => {
    const { values, component } = render(fieldValue());

    component.numberForm.patchValue({ min: -100, max: 999 });

    expect(component.numberForm.value).toEqual({ min: -100, max: 999 });
    expect(values.get('price')!.value).toBe('-100-999');
  });

  it('clamps a typed minimum up to the configured lower bound', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'price', displayName: 'Price', fieldTypeName: 'Number',
        configuration: { 'Number.Min': 10, 'Number.Max': 100 },
      },
    });
    const { component } = render(field);

    component.numberForm.patchValue({ min: 5 });

    expect(component.numberForm.value.min).toBe(10);
  });

  it('clamps a typed maximum down to the configured upper bound', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'price', displayName: 'Price', fieldTypeName: 'Number',
        configuration: { 'Number.Min': 10, 'Number.Max': 100 },
      },
    });
    const { component } = render(field);

    component.numberForm.patchValue({ max: 500 });

    expect(component.numberForm.value.max).toBe(100);
  });

  it('clamps a crossed range by pulling the minimum down to the maximum', () => {
    const { component } = render(fieldValue());

    component.numberForm.patchValue({ min: 50, max: 20 });

    expect(component.numberForm.value).toEqual({ min: 20, max: 20 });
  });
});
