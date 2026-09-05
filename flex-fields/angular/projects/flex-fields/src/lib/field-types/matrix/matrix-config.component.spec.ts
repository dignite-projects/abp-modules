import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldData } from '../../models';
import { provideFlexFields } from '../../providers';
import { COMPOSITE_NESTING_DEPTH, MAX_COMPOSITE_NESTING_DEPTH } from '../composite-nesting';
import { MatrixConfigComponent } from './matrix-config.component';

function fieldData(overrides: Partial<FlexFieldData> = {}): FlexFieldData {
  return {
    id: '1',
    name: 'sections',
    displayName: 'Sections',
    fieldTypeName: 'Matrix',
    configuration: {},
    ...overrides,
  };
}

const STORED_BLOCK_TYPES = [
  {
    name: 'quote',
    displayName: 'Quote',
    fields: [
      {
        name: 'text',
        displayName: 'Text',
        fieldTypeName: 'Text',
        required: true,
        configuration: { 'Text.CharLimit': 120 },
      },
    ],
  },
  { name: 'image', displayName: 'Image', fields: [] },
];

function render(selected?: FlexFieldData) {
  const entity = new FormGroup({});
  const fixture = TestBed.createComponent(MatrixConfigComponent);
  fixture.componentRef.setInput('type', 'Matrix');
  fixture.componentRef.setInput('Entity', entity);
  if (selected) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { fixture, entity, component: fixture.componentInstance };
}

describe('MatrixConfigComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
      providers: [provideFlexFields()],
    });
  });

  it('starts a new field with no block types at all', () => {
    // Unlike Select's one blank option there is no sensible block type to guess at, so nothing is
    // seeded and there is nothing selected to show.
    const { component, entity } = render();

    expect(component.blockTypes.length).toBe(0);
    expect(component.selectedBlockTypeIndex).toBeNull();
    expect(component.selectedSubFieldIndex).toBeNull();
    expect(entity.get(['configuration', 'Matrix.BlockTypes'])).toBeTruthy();
  });

  it('patches stored Matrix.BlockTypes into the form arrays, sub-fields included', () => {
    const { component } = render(fieldData({ configuration: { 'Matrix.BlockTypes': STORED_BLOCK_TYPES } }));

    expect(component.blockTypes.length).toBe(2);
    expect(component.blockTypes.at(0).value.name).toBe('quote');
    expect(component.blockTypes.at(0).value.displayName).toBe('Quote');
    expect(component.blockTypes.at(1).value.fields).toEqual([]);

    const fields = component.fieldsOf(component.blockTypes.at(0));
    expect(fields.length).toBe(1);
    expect(fields.at(0).value).toMatchObject({
      name: 'text',
      displayName: 'Text',
      description: '',
      fieldTypeName: 'Text',
      required: true,
    });
  });

  it('hands each sub-field its stored configuration to the recursively-mounted editor', () => {
    const { component } = render(fieldData({ configuration: { 'Matrix.BlockTypes': STORED_BLOCK_TYPES } }));

    // The nested <ff-flex-field-config> replaced the seeded `configuration` control with Text's own
    // group and patched the stored value into it - that round trip is the whole recursion mechanism.
    const subField = component.fieldsOf(component.blockTypes.at(0)).at(0);
    expect(subField.get('configuration')!.value['Text.CharLimit']).toBe(120);
  });

  it('patches a PascalCase stored configuration in just the same way', () => {
    // A Matrix field seeded server-side from the typed C# configuration classes comes back PascalCase
    // (EF Core serializes the value converter's JSON with System.Text.Json's default options), so the
    // designer has to load it too - otherwise the block types render as if there were none.
    const { component } = render(
      fieldData({
        configuration: {
          'Matrix.BlockTypes': [
            {
              Name: 'quote',
              DisplayName: 'Quote',
              Fields: [
                {
                  Name: 'text',
                  DisplayName: 'Text',
                  FieldTypeName: 'Text',
                  Required: true,
                  Configuration: { 'Text.CharLimit': 120 },
                },
              ],
            },
            { Name: 'image', DisplayName: 'Image', Fields: [] },
          ],
        },
      }),
    );

    expect(component.blockTypes.length).toBe(2);
    expect(component.blockTypes.at(0).value.name).toBe('quote');
    expect(component.blockTypes.at(1).value.name).toBe('image');

    const fields = component.fieldsOf(component.blockTypes.at(0));
    expect(fields.length).toBe(1);
    expect(fields.at(0).value).toMatchObject({ name: 'text', fieldTypeName: 'Text', required: true });
    expect(fields.at(0).get('configuration')!.value['Text.CharLimit']).toBe(120);
  });

  it('selects the first block type and its first field after loading, not the last', () => {
    const { component } = render(fieldData({ configuration: { 'Matrix.BlockTypes': STORED_BLOCK_TYPES } }));

    expect(component.selectedBlockTypeIndex).toBe(0);
    expect(component.selectedSubFieldIndex).toBe(0);
  });

  it('does not leak configuration from a field of a different type', () => {
    const { component } = render(
      fieldData({ fieldTypeName: 'Text', configuration: { 'Matrix.BlockTypes': STORED_BLOCK_TYPES } }),
    );

    expect(component.blockTypes.length).toBe(0);
  });

  it('keeps the selection sane as block types are added and removed', () => {
    const { component } = render();

    component.addBlockType();
    expect(component.selectedBlockTypeIndex).toBe(0);
    expect(component.selectedSubFieldIndex).toBeNull();

    component.addBlockType();
    expect(component.blockTypes.length).toBe(2);
    expect(component.selectedBlockTypeIndex).toBe(1);

    // Removing the selected, last block type falls back to the one before it.
    component.removeBlockType(1);
    expect(component.blockTypes.length).toBe(1);
    expect(component.selectedBlockTypeIndex).toBe(0);

    component.removeBlockType(0);
    expect(component.selectedBlockTypeIndex).toBeNull();
    expect(component.selectedSubFieldIndex).toBeNull();
  });

  it('keeps the selection sane as a block type\'s own fields are added and removed', () => {
    const { component } = render();
    const blockType = component.addBlockType();

    component.addSubField(blockType);
    component.addSubField(blockType);
    expect(component.selectedSubFieldIndex).toBe(1);

    // Removing something above the selection shifts it down rather than leaving it pointing past the end.
    component.removeSubField(blockType, 0);
    expect(component.fieldsOf(blockType).length).toBe(1);
    expect(component.selectedSubFieldIndex).toBe(0);

    component.removeSubField(blockType, 0);
    expect(component.selectedSubFieldIndex).toBeNull();
  });

  it('offers every registered type, composites included, to a top-level Matrix', () => {
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
      // What an enclosing composite config editor would have provided: this Matrix's own sub-fields
      // then land at MAX_COMPOSITE_NESTING_DEPTH, with no room left under them.
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

    it('still shows a composite the selected sub-field is already bound to', () => {
      // Saving it fails on the server either way - CompositeFieldNesting is the constraint - but an
      // empty select would hide what the field actually is.
      const { component } = render(
        fieldData({
          configuration: {
            'Matrix.BlockTypes': [
              {
                name: 'quote',
                displayName: 'Quote',
                fields: [
                  { name: 'rows', displayName: 'Rows', fieldTypeName: 'Table', required: false, configuration: {} },
                ],
              },
            ],
          },
        }),
      );

      expect(component.fieldTypeOptions.map(fieldType => fieldType.name)).toContain('Table');
      expect(component.fieldTypeOptions.map(fieldType => fieldType.name)).not.toContain('Matrix');
    });
  });
});
