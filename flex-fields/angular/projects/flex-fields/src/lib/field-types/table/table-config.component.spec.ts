import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldData } from '../../models';
import { provideFlexFields } from '../../providers';
import { COMPOSITE_NESTING_DEPTH, MAX_COMPOSITE_NESTING_DEPTH } from '../composite-nesting';
import { TableConfigComponent } from './table-config.component';

function fieldData(overrides: Partial<FlexFieldData> = {}): FlexFieldData {
  return {
    id: '1',
    name: 'specs',
    displayName: 'Specs',
    fieldTypeName: 'Table',
    configuration: {},
    ...overrides,
  };
}

const STORED_COLUMNS = [
  {
    name: 'title',
    displayName: 'Title',
    fieldTypeName: 'Text',
    required: true,
    configuration: { 'Text.CharLimit': 120 },
  },
  { name: 'qty', displayName: 'Quantity', fieldTypeName: 'Number', required: false, configuration: {} },
];

function render(selected?: FlexFieldData) {
  const entity = new FormGroup({});
  const fixture = TestBed.createComponent(TableConfigComponent);
  fixture.componentRef.setInput('type', 'Table');
  fixture.componentRef.setInput('Entity', entity);
  if (selected) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { fixture, entity, component: fixture.componentInstance };
}

describe('TableConfigComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
      providers: [provideFlexFields()],
    });
  });

  it('starts a new field with one blank column, not an empty schema', () => {
    // The same "seed one row" convention SelectConfigComponent uses for its option list. Matrix does
    // the opposite (no block types) because there is no sensible default block type to guess at.
    const { component, entity } = render();

    expect(component.columns.length).toBe(1);
    expect(component.selectedColumnIndex).toBe(0);
    expect(component.columns.at(0).value).toMatchObject({
      name: '',
      displayName: '',
      description: '',
      fieldTypeName: '',
      required: false,
    });
    expect(entity.get(['configuration', 'Table.Columns'])).toBeTruthy();
  });

  it('patches stored Table.Columns into the form array', () => {
    const { component } = render(fieldData({ configuration: { 'Table.Columns': STORED_COLUMNS } }));

    expect(component.columns.length).toBe(2);
    expect(component.columns.at(0).value).toMatchObject({
      name: 'title',
      displayName: 'Title',
      description: '',
      fieldTypeName: 'Text',
      required: true,
    });
    expect(component.columns.at(1).value).toMatchObject({ name: 'qty', fieldTypeName: 'Number' });
    expect(component.selectedColumnIndex).toBe(0);
  });

  it('patches a PascalCase stored configuration in just the same way', () => {
    // A Table field seeded server-side from the typed C# configuration classes comes back PascalCase
    // (EF Core serializes the value converter's JSON with System.Text.Json's default options), so the
    // designer has to load it too - otherwise the columns render as if there were none.
    const { component } = render(
      fieldData({
        configuration: {
          'Table.Columns': [
            {
              Name: 'title',
              DisplayName: 'Title',
              FieldTypeName: 'Text',
              Required: true,
              Configuration: { 'Text.CharLimit': 120 },
            },
            { Name: 'qty', DisplayName: 'Quantity', FieldTypeName: 'Number', Required: false },
          ],
        },
      }),
    );

    expect(component.columns.length).toBe(2);
    expect(component.columns.at(0).value).toMatchObject({
      name: 'title',
      displayName: 'Title',
      fieldTypeName: 'Text',
      required: true,
    });
    expect(component.columns.at(1).value).toMatchObject({ name: 'qty', fieldTypeName: 'Number' });
    expect(component.columns.at(0).get('configuration')!.value['Text.CharLimit']).toBe(120);
  });

  it('hands each column its stored configuration to the recursively-mounted editor', () => {
    const { component } = render(fieldData({ configuration: { 'Table.Columns': STORED_COLUMNS } }));

    // The nested <ff-flex-field-config> replaced the seeded `configuration` control with Text's own
    // group and patched the stored value into it - that round trip is the whole recursion mechanism.
    expect(component.columns.at(0).get('configuration')!.value['Text.CharLimit']).toBe(120);
  });

  it('keeps an unopened column\'s stored configuration rather than dropping it on save', () => {
    // Only the selected column mounts a nested editor; the second column's raw configuration has to
    // survive on the seeded FormControl or saving would silently blank it.
    const { component } = render(
      fieldData({
        configuration: {
          'Table.Columns': [
            STORED_COLUMNS[0],
            { ...STORED_COLUMNS[1], configuration: { 'Number.Decimals': 2 } },
          ],
        },
      }),
    );

    expect(component.columns.at(1).get('configuration')!.value).toEqual({ 'Number.Decimals': 2 });
  });

  it('does not leak configuration from a field of a different type', () => {
    const { component } = render(
      fieldData({ fieldTypeName: 'Text', configuration: { 'Table.Columns': STORED_COLUMNS } }),
    );

    expect(component.columns.length).toBe(1);
    expect(component.columns.at(0).value.name).toBe('');
  });

  it('keeps the selection sane as columns are added and removed', () => {
    const { component } = render();
    expect(component.columns.length).toBe(1);

    component.addColumn();
    component.addColumn();
    expect(component.columns.length).toBe(3);
    expect(component.selectedColumnIndex).toBe(2);

    // Removing something above the selection shifts it down rather than leaving it past the end.
    component.removeColumn(0);
    expect(component.selectedColumnIndex).toBe(1);

    // Removing the selected, last column falls back to the one before it.
    component.removeColumn(1);
    expect(component.selectedColumnIndex).toBe(0);

    component.removeColumn(0);
    expect(component.columns.length).toBe(0);
    expect(component.selectedColumnIndex).toBeNull();
  });

  it('offers every registered type, composites included, to a top-level Table', () => {
    const { component } = render();

    expect(component.fieldTypeOptions.map(fieldType => fieldType.name)).toEqual([
      'Text',
      'Number',
      'DateTime',
      'Select',
      'Boolean',
      'Tree',
      'Matrix',
      'Table',
    ]);
  });

  describe('mounted at the nesting limit', () => {
    beforeEach(() => {
      // What an enclosing composite config editor would have provided: this Table's own columns then
      // land at MAX_COMPOSITE_NESTING_DEPTH, with no room left under them.
      TestBed.configureTestingModule({
        providers: [{ provide: COMPOSITE_NESTING_DEPTH, useValue: MAX_COMPOSITE_NESTING_DEPTH - 1 }],
      });
    });

    it('stops offering composite types', () => {
      const { component } = render();

      expect(component.fieldTypeOptions.map(fieldType => fieldType.name)).toEqual([
        'Text',
        'Number',
        'DateTime',
        'Select',
        'Boolean',
        'Tree',
      ]);
    });

    it('still shows a composite the selected column is already bound to', () => {
      // Saving it fails on the server either way - CompositeFieldNesting is the constraint - but an
      // empty select would hide what the column actually is.
      const { component } = render(
        fieldData({
          configuration: {
            'Table.Columns': [
              { name: 'blocks', displayName: 'Blocks', fieldTypeName: 'Matrix', required: false, configuration: {} },
            ],
          },
        }),
      );

      expect(component.fieldTypeOptions.map(fieldType => fieldType.name)).toContain('Matrix');
      expect(component.fieldTypeOptions.map(fieldType => fieldType.name)).not.toContain('Table');
    });
  });
});
