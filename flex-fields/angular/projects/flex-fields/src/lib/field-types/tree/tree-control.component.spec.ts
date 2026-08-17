import { FormGroup, Validators } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '../../models';
import { TreeControlComponent } from './tree-control.component';

const NODE_ITEMS = [
  {
    Text: 'Fruit',
    Value: 'fruit',
    Selected: false,
    Children: [
      { Text: 'Apple', Value: 'apple', Selected: false, Children: [] },
      { Text: 'Banana', Value: 'banana', Selected: true, Children: [] },
    ],
  },
];

function fieldValue(overrides: Partial<FlexFieldValue> = {}): FlexFieldValue {
  return {
    field: {
      id: '1',
      name: 'category',
      displayName: 'Category',
      fieldTypeName: 'Tree',
      configuration: { 'Tree.Multiple': false, 'Tree.Nodes': NODE_ITEMS },
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

function render(field: FlexFieldValue, selected?: unknown) {
  const values = new FormGroup({});
  const entity = new FormGroup({ flexFields: values });
  const fixture = TestBed.createComponent(TreeControlComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  if (selected !== undefined) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { fixture, values, component: fixture.componentInstance };
}

describe('TreeControlComponent', () => {
  it('renders the shared tree picker', () => {
    const { fixture } = render(fieldValue());
    expect(fixture.nativeElement.querySelector('ff-tree-picker-nodes')).toBeTruthy();
  });

  it('falls back to the node marked Selected in the configuration when nothing is stored', () => {
    const { values } = render(fieldValue());
    expect(values.get('category')!.value).toBe('banana');
  });

  it('uses the stored value over the configured default', () => {
    const { values } = render(fieldValue(), 'fruit');
    expect(values.get('category')!.value).toBe('fruit');
  });

  it('collects every stored key in multiple mode', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'category', displayName: 'Category', fieldTypeName: 'Tree',
        configuration: { 'Tree.Multiple': true, 'Tree.Nodes': NODE_ITEMS },
      },
    });
    const { values } = render(field, ['apple', 'banana']);
    expect(values.get('category')!.value).toEqual(['apple', 'banana']);
  });

  it('adds a required validator when the usage requires the field', () => {
    const { values } = render(fieldValue({ required: true }));
    expect(values.get('category')!.hasValidator(Validators.required)).toBe(true);
  });

  it('replaces the single selection when a different node is toggled', () => {
    const { values, component } = render(fieldValue(), 'fruit');

    component.toggleSelected('apple');

    expect(values.get('category')!.value).toBe('apple');
    expect(component.selectedKeys).toEqual(['apple']);
  });

  it('checks a selected child\'s ancestors too in multiple mode', () => {
    // A fresh set of nodes, none pre-Selected: NODE_ITEMS' own Banana:Selected default would
    // otherwise also land in the initial selection and muddy what this test is isolating.
    const noDefaultsSelected = [
      {
        Text: 'Fruit', Value: 'fruit', Selected: false,
        Children: [
          { Text: 'Apple', Value: 'apple', Selected: false, Children: [] },
          { Text: 'Banana', Value: 'banana', Selected: false, Children: [] },
        ],
      },
    ];
    const field = fieldValue({
      field: {
        id: '1', name: 'category', displayName: 'Category', fieldTypeName: 'Tree',
        configuration: { 'Tree.Multiple': true, 'Tree.Nodes': noDefaultsSelected },
      },
    });
    const { values, component } = render(field);

    component.toggleSelected('apple');

    expect(component.selectedKeys.sort()).toEqual(['apple', 'fruit']);
    expect((values.get('category')!.value as string[]).sort()).toEqual(['apple', 'fruit']);
  });

  it('clears the selection', () => {
    const { values, component } = render(fieldValue(), 'fruit');

    component.clearSelection();

    expect(values.get('category')!.value).toBeNull();
    expect(component.selectedKeys).toEqual([]);
  });
});
