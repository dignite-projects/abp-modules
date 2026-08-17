import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '../../models';
import { SelectViewComponent } from './select-view.component';

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
      configuration: { 'Select.Options': OPTIONS },
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

describe('SelectViewComponent', () => {
  function render(fields: FlexFieldValue, value: unknown, showInList = false) {
    const fixture = TestBed.createComponent(SelectViewComponent);
    fixture.componentRef.setInput('fields', fields);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('showInList', showInList);
    fixture.detectChanges();
    return fixture;
  }

  it('shows the option label for a stored value, not the raw value', () => {
    const fixture = render(fieldValue(), 'red');
    expect(fixture.nativeElement.textContent).toContain('Red');
    expect(fixture.nativeElement.textContent).not.toContain('red');
  });

  it('joins every selected label for a multi-valued field', () => {
    const fixture = render(fieldValue(), ['red', 'blue']);
    expect(fixture.componentInstance.displayValue).toBe('Red, Blue');
  });

  it('falls back to the raw stored value when it matches no option', () => {
    const fixture = render(fieldValue(), 'purple');
    expect(fixture.componentInstance.displayValue).toBe('purple');
  });

  it('renders bare in list mode and inside a label wrapper otherwise', () => {
    expect(render(fieldValue(), 'red', true).nativeElement.textContent).toContain('Red');
    expect(render(fieldValue(), 'red', false).nativeElement.textContent).toContain('Red');
  });
});
