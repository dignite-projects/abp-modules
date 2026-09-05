import { FormArray, FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldValue } from '../../models';
import { provideFlexFields } from '../../providers';
import { TextMode } from '../text';
import { TableControlComponent } from './table-control.component';

const COLUMNS = [
  {
    name: 'title',
    displayName: 'Title',
    fieldTypeName: 'Text',
    required: false,
    configuration: { 'Text.Mode': TextMode.SingleLine },
  },
  {
    name: 'note',
    displayName: 'Note',
    fieldTypeName: 'Text',
    required: false,
    configuration: { 'Text.Mode': TextMode.SingleLine },
  },
];

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'specs',
      displayName: 'Specs',
      fieldTypeName: 'Table',
      configuration: { 'Table.Columns': COLUMNS },
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

function render(field: FlexFieldValue, selected?: unknown) {
  const values = new FormGroup({});
  const entity = new FormGroup({ flexFields: values });
  const fixture = TestBed.createComponent(TableControlComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  if (selected !== undefined) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { fixture, values, component: fixture.componentInstance };
}

describe('TableControlComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
      providers: [provideFlexFields()],
    });
  });

  it('binds a FormArray, not a scalar control', () => {
    const { values } = render(fieldValue());
    expect(values.get('specs')).toBeInstanceOf(FormArray);
    expect(values.get('specs')!.value).toEqual([]);
  });

  it('renders one header per configured column', () => {
    const { component, fixture } = render(fieldValue());

    expect(component.columns.map(column => column.name)).toEqual(['title', 'note']);
    const headers = [...fixture.nativeElement.querySelectorAll('thead th')].map((th: HTMLElement) =>
      th.textContent!.trim(),
    );
    expect(headers).toContain('Title');
    expect(headers).toContain('Note');
  });

  it('renders stored rows and recursively mounts each column control', () => {
    const { component, values } = render(fieldValue(), [
      { values: { title: 'One', note: 'first' } },
      { values: { title: 'Two', note: 'second' } },
    ]);

    expect(component.rows.length).toBe(2);
    expect(component.valuesGroupOf(component.rows.at(0)).get('title')!.value).toBe('One');
    expect(values.get('specs')!.value).toEqual([
      { values: { title: 'One', note: 'first' } },
      { values: { title: 'Two', note: 'second' } },
    ]);
  });

  it('grows the value the form emits when a row is added', () => {
    const { component, values, fixture } = render(fieldValue(), [{ values: { title: 'One', note: 'first' } }]);

    component.addRow();
    fixture.detectChanges();

    expect(component.rows.length).toBe(2);
    expect(values.get('specs')!.value).toEqual([
      { values: { title: 'One', note: 'first' } },
      { values: { title: '', note: '' } },
    ]);
  });

  it('shrinks it again when a row is removed', () => {
    const { component, values, fixture } = render(fieldValue(), [
      { values: { title: 'One', note: 'first' } },
      { values: { title: 'Two', note: 'second' } },
    ]);

    component.removeRow(0);
    fixture.detectChanges();

    expect(values.get('specs')!.value).toEqual([{ values: { title: 'Two', note: 'second' } }]);
  });

  it('marks a required Table invalid until it holds at least one row', () => {
    const { component, values, fixture } = render(fieldValue({ required: true }));
    expect(values.get('specs')!.errors).toEqual({ required: true });

    component.addRow();
    fixture.detectChanges();
    expect(values.get('specs')!.errors).toBeNull();
  });

  it('reads a stored value that is not an array as no rows at all', () => {
    const { component } = render(fieldValue(), 'not-an-array');
    expect(component.rows.length).toBe(0);
  });
});
