import { FormGroup, Validators } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '../../models';
import { TextSearchComponent } from './text-search.component';

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'title',
      displayName: 'Title',
      fieldTypeName: 'Text',
      configuration: { 'Text.CharLimit': 5 },
    },
    required: false,
    searchable: true,
    ...overrides,
  };
}

function render(field: FlexFieldValue, selected?: unknown) {
  const values = new FormGroup({});
  const entity = new FormGroup({ flexFields: values });
  const fixture = TestBed.createComponent(TextSearchComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  if (selected !== undefined) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { values };
}

describe('TextSearchComponent', () => {
  it('uses the selected value', () => {
    const { values } = render(fieldValue(), 'partial match');
    expect(values.get('title')!.value).toBe('partial match');
  });

  it('ignores the configured character limit - a filter is not a value', () => {
    const { values } = render(fieldValue(), 'this search text is longer than five characters');
    expect(values.get('title')!.hasError('maxlength')).toBe(false);
  });

  it('adds no required validator even when the field usage requires it', () => {
    const { values } = render(fieldValue({ required: true }));
    expect(values.get('title')!.hasValidator(Validators.required)).toBe(false);
  });
});
