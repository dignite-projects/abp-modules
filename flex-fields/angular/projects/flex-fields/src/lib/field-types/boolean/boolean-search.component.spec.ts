import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { FlexFieldValue } from '../../models';
import { BooleanSearchComponent } from './boolean-search.component';

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'isActive',
      displayName: 'Active',
      fieldTypeName: 'Boolean',
      configuration: { 'Boolean.Default': true },
    },
    required: false,
    searchable: true,
    ...overrides,
  };
}

describe('BooleanSearchComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig()],
    });
  });

  function render(field: FlexFieldValue, selected?: unknown) {
    const values = new FormGroup({});
    const entity = new FormGroup({ flexFields: values });
    const fixture = TestBed.createComponent(BooleanSearchComponent);
    fixture.componentRef.setInput('fields', field);
    fixture.componentRef.setInput('entity', entity);
    fixture.componentRef.setInput('parentFieldName', 'flexFields');
    if (selected !== undefined) {
      fixture.componentRef.setInput('selected', selected);
    }
    fixture.detectChanges();
    return { fixture, values };
  }

  it('starts unset rather than falling back to the configured default', () => {
    const { values } = render(fieldValue());
    expect(values.get('isActive')!.value).toBe('');
  });

  it('seeds the control with a selected true value', () => {
    expect(render(fieldValue(), true).values.get('isActive')!.value).toBe(true);
  });

  it('seeds the control with a selected false value', () => {
    expect(render(fieldValue(), false).values.get('isActive')!.value).toBe(false);
  });

  it('adds no required validator even when the field usage requires it', () => {
    // A filter must stay optional - "required" governs the edit form, not the search bar.
    const { values } = render(fieldValue({ required: true }));
    expect(values.get('isActive')!.validator).toBeNull();
  });
});
