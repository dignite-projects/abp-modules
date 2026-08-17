import { FormGroup, Validators } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '../../models';
import { DateTimeControlComponent } from './date-time-control.component';
import { DateTimeInputMode } from './date-time-input-mode';

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'publishedAt',
      displayName: 'Published',
      fieldTypeName: 'DateTime',
      configuration: { 'DateTime.InputMode': DateTimeInputMode.Date },
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

function withMode(mode: DateTimeInputMode, overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return fieldValue({
    field: {
      id: '1',
      name: 'publishedAt',
      displayName: 'Published',
      fieldTypeName: 'DateTime',
      configuration: { 'DateTime.InputMode': mode, 'DateTime.Min': '2026-01-01', 'DateTime.Max': '2026-12-31' },
    },
    ...overrides,
  });
}

function render(field: FlexFieldValue, selected?: unknown) {
  const values = new FormGroup({});
  const entity = new FormGroup({ flexFields: values });
  const fixture = TestBed.createComponent(DateTimeControlComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  if (selected !== undefined) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { fixture, values };
}

describe('DateTimeControlComponent', () => {
  // Local-time construction, not an ISO `Z` string: DatePipe formats in local time, so building the
  // expectation from the same local wall-clock value keeps the test independent of the machine's TZ.
  const localDateTime = new Date(2026, 7, 17, 10, 30, 0);

  it('formats a selected value to the Date input mode', () => {
    const { values } = render(withMode(DateTimeInputMode.Date), localDateTime);
    expect(values.get('publishedAt')!.value).toBe('2026-08-17');
  });

  it('formats a selected value to the DateTime input mode', () => {
    const { values } = render(withMode(DateTimeInputMode.DateTime), localDateTime);
    expect(values.get('publishedAt')!.value).toBe('2026-08-17 10:30:00');
  });

  it('formats a selected value to the Month input mode', () => {
    const { values } = render(withMode(DateTimeInputMode.Month), localDateTime);
    expect(values.get('publishedAt')!.value).toBe('2026-08');
  });

  it('leaves the control unset rather than throwing when nothing is selected yet', () => {
    // A brand-new record has no value for the field - DatePipe must degrade gracefully (null), not
    // throw, or the create form would crash before the user typed anything.
    const { values } = render(fieldValue());
    expect(values.get('publishedAt')!.value).toBeNull();
  });

  it('adds a required validator when the usage requires the field', () => {
    const { values } = render(fieldValue({ required: true }), localDateTime);
    expect(values.get('publishedAt')!.hasValidator(Validators.required)).toBe(true);
  });

  it('adds no validator when the usage does not require the field', () => {
    const { values } = render(fieldValue({ required: false }), localDateTime);
    expect(values.get('publishedAt')!.hasValidator(Validators.required)).toBe(false);
  });

  it('renders the input type and min/max bounds matching the configured mode', () => {
    const { fixture } = render(withMode(DateTimeInputMode.DateTime), localDateTime);
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');

    expect(input.type).toBe('datetime-local');
    expect(input.min).toBe('2026-01-01');
    expect(input.max).toBe('2026-12-31');
  });

  it('renders exactly one input for the configured mode, not all three', () => {
    const { fixture } = render(withMode(DateTimeInputMode.Month), localDateTime);
    expect(fixture.nativeElement.querySelectorAll('input')).toHaveLength(1);
  });
});
