import { FormGroup, Validators } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '../../models';
import { TextControlComponent } from './text-control.component';
import { TextMode } from './text-mode';

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'title',
      displayName: 'Title',
      fieldTypeName: 'Text',
      configuration: { 'Text.Mode': TextMode.SingleLine, 'Text.Placeholder': 'Enter a title' },
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

function render(field: FlexFieldValue, selected?: unknown) {
  const values = new FormGroup({});
  const entity = new FormGroup({ flexFields: values });
  const fixture = TestBed.createComponent(TextControlComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  if (selected !== undefined) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { fixture, values };
}

describe('TextControlComponent', () => {
  it('renders a single-line input in SingleLine mode', () => {
    const { fixture } = render(fieldValue());
    expect(fixture.nativeElement.querySelector('input[type="text"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('textarea')).toBeFalsy();
  });

  it('renders a textarea in MultipleLine mode', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'title', displayName: 'Title', fieldTypeName: 'Text',
        configuration: { 'Text.Mode': TextMode.MultipleLine },
      },
    });
    const { fixture } = render(field);
    expect(fixture.nativeElement.querySelector('textarea')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('input[type="text"]')).toBeFalsy();
  });

  it('reflects the configured placeholder', () => {
    const { fixture } = render(fieldValue());
    const input: HTMLInputElement = fixture.nativeElement.querySelector('input');
    expect(input.placeholder).toBe('Enter a title');
  });

  it('uses the selected value', () => {
    const { values } = render(fieldValue(), 'Hello world');
    expect(values.get('title')!.value).toBe('Hello world');
  });

  it('adds a required validator when the usage requires the field', () => {
    const { values } = render(fieldValue({ required: true }));
    expect(values.get('title')!.hasValidator(Validators.required)).toBe(true);
  });

  it('enforces the configured character limit', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'title', displayName: 'Title', fieldTypeName: 'Text',
        configuration: { 'Text.CharLimit': 5 },
      },
    });
    const { values } = render(field, '123456');
    expect(values.get('title')!.hasError('maxlength')).toBe(true);
  });

  it('does not flag a value within the configured character limit', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'title', displayName: 'Title', fieldTypeName: 'Text',
        configuration: { 'Text.CharLimit': 5 },
      },
    });
    const { values } = render(field, '123');
    expect(values.get('title')!.hasError('maxlength')).toBe(false);
  });
});
