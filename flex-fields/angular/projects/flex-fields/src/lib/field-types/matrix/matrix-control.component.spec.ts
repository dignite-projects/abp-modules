import { FormArray, FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldValue } from '../../models';
import { provideFlexFields } from '../../providers';
import { TextMode } from '../text';
import { MatrixControlComponent } from './matrix-control.component';

const BLOCK_TYPES = [
  {
    name: 'quote',
    displayName: 'Quote',
    fields: [
      {
        name: 'text',
        displayName: 'Text',
        fieldTypeName: 'Text',
        required: false,
        configuration: { 'Text.Mode': TextMode.SingleLine },
      },
    ],
  },
  { name: 'divider', displayName: 'Divider', fields: [] },
];

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'sections',
      displayName: 'Sections',
      fieldTypeName: 'Matrix',
      configuration: { 'Matrix.BlockTypes': BLOCK_TYPES },
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

function render(field: FlexFieldValue, selected?: unknown) {
  const values = new FormGroup({});
  const entity = new FormGroup({ flexFields: values });
  const fixture = TestBed.createComponent(MatrixControlComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  if (selected !== undefined) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { fixture, values, component: fixture.componentInstance };
}

describe('MatrixControlComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
      providers: [provideFlexFields()],
    });
  });

  it('binds a FormArray, not a scalar control', () => {
    const { values } = render(fieldValue());
    expect(values.get('sections')).toBeInstanceOf(FormArray);
    expect(values.get('sections')!.value).toEqual([]);
  });

  it('renders the configured block types as add buttons', () => {
    const { component, fixture } = render(fieldValue());

    expect(component.blockTypes.map(blockType => blockType.name)).toEqual(['quote', 'divider']);
    expect(fixture.nativeElement.textContent).toContain('Quote');
    expect(fixture.nativeElement.textContent).toContain('Divider');
  });

  it('renders stored blocks and recursively mounts each sub-field control', () => {
    const { component, values } = render(fieldValue(), [
      { blockTypeName: 'quote', values: { text: 'hello' } },
      { blockTypeName: 'divider', values: {} },
    ]);

    expect(component.blocks.length).toBe(2);

    // The recursion put the stored sub-field value on a real control inside the block's own group.
    const block = component.blocks.at(0);
    expect(component.valuesGroupOf(block).get('text')!.value).toBe('hello');
    expect(values.get('sections')!.value).toEqual([
      { blockTypeName: 'quote', values: { text: 'hello' } },
      { blockTypeName: 'divider', values: {} },
    ]);
  });

  it('grows the value the form emits when a block is added', () => {
    const { component, values, fixture } = render(fieldValue(), [
      { blockTypeName: 'quote', values: { text: 'hello' } },
    ]);

    component.addBlock(component.blockTypes[0]);
    fixture.detectChanges();

    expect(component.blocks.length).toBe(2);
    expect(values.get('sections')!.value).toEqual([
      { blockTypeName: 'quote', values: { text: 'hello' } },
      { blockTypeName: 'quote', values: { text: '' } },
    ]);
  });

  it('shrinks it again when a block is removed', () => {
    const { component, values, fixture } = render(fieldValue(), [
      { blockTypeName: 'quote', values: { text: 'hello' } },
      { blockTypeName: 'divider', values: {} },
    ]);

    component.removeBlock(0);
    fixture.detectChanges();

    expect(values.get('sections')!.value).toEqual([{ blockTypeName: 'divider', values: {} }]);
  });

  it('starts a newly added block expanded and a loaded one collapsed', () => {
    // UI state only - never written into the value.
    const { component, fixture } = render(fieldValue(), [{ blockTypeName: 'quote', values: {} }]);
    expect(component.isExpanded(component.blocks.at(0))).toBe(false);

    component.addBlock(component.blockTypes[0]);
    fixture.detectChanges();
    expect(component.isExpanded(component.blocks.at(1))).toBe(true);

    component.toggleExpanded(component.blocks.at(1));
    expect(component.isExpanded(component.blocks.at(1))).toBe(false);
  });

  it('marks a required Matrix invalid until it holds at least one block', () => {
    const { component, values, fixture } = render(fieldValue({ required: true }));
    expect(values.get('sections')!.errors).toEqual({ required: true });

    component.addBlock(component.blockTypes[0]);
    fixture.detectChanges();
    expect(values.get('sections')!.errors).toBeNull();
  });

  it('reads a stored value that is not an array as no blocks at all', () => {
    const { component } = render(fieldValue(), 'not-an-array');
    expect(component.blocks.length).toBe(0);
  });
});
