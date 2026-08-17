import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '../../models';
import { TreeSearchComponent } from './tree-search.component';

const NODE_ITEMS = [
  {
    Text: 'Fruit',
    Value: 'fruit',
    Selected: false,
    Children: [
      { Text: 'Apple', Value: 'apple', Selected: true, Children: [] },
      { Text: 'Banana', Value: 'banana', Selected: false, Children: [] },
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
    searchable: true,
    ...overrides,
  };
}

function render(field: FlexFieldValue, selected?: unknown) {
  const values = new FormGroup({});
  const entity = new FormGroup({ flexFields: values });
  const fixture = TestBed.createComponent(TreeSearchComponent);
  fixture.componentRef.setInput('fields', field);
  fixture.componentRef.setInput('entity', entity);
  fixture.componentRef.setInput('parentFieldName', 'flexFields');
  if (selected !== undefined) {
    fixture.componentRef.setInput('selected', selected);
  }
  fixture.detectChanges();
  return { fixture, values, component: fixture.componentInstance };
}

describe('TreeSearchComponent', () => {
  it('does not fall back to the node marked Selected - a default answer is not a default filter', () => {
    const { values } = render(fieldValue());
    expect(values.get('category')!.value).toBeNull();
  });

  it('starts empty in multiple mode rather than pre-filtering by the Selected defaults', () => {
    const field = fieldValue({
      field: {
        id: '1', name: 'category', displayName: 'Category', fieldTypeName: 'Tree',
        configuration: { 'Tree.Multiple': true, 'Tree.Nodes': NODE_ITEMS },
      },
    });
    const { values } = render(field);
    expect(values.get('category')!.value).toEqual([]);
  });

  it('uses the stored value', () => {
    const { values } = render(fieldValue(), 'banana');
    expect(values.get('category')!.value).toBe('banana');
  });

  it('displays the labels of the selected nodes, joined', () => {
    const { component } = render(fieldValue(), 'banana');
    expect(component.displayText).toBe('Banana');
  });

  it('toggles the dropdown open state', () => {
    const { component } = render(fieldValue());
    expect(component.isDropdownOpen).toBe(false);

    component.toggleDropdown();
    expect(component.isDropdownOpen).toBe(true);

    component.toggleDropdown();
    expect(component.isDropdownOpen).toBe(false);
  });

  it('closes the dropdown once a single value is picked', () => {
    const { component } = render(fieldValue());
    component.toggleDropdown();
    expect(component.isDropdownOpen).toBe(true);

    component.toggleSelected('apple');

    expect(component.isDropdownOpen).toBe(false);
  });

  it('clears the selection without an event', () => {
    const { values, component } = render(fieldValue(), 'banana');

    component.clearSelection();

    expect(values.get('category')!.value).toBeNull();
  });
});
