import { TestBed } from '@angular/core/testing';
import { FlexFieldValue } from '../../models';
import { TreeViewComponent } from './tree-view.component';

const NODE_ITEMS = [
  {
    Text: 'Fruit',
    Value: 'fruit',
    Selected: false,
    Children: [
      { Text: 'Apple', Value: 'apple', Selected: false, Children: [] },
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
      configuration: { 'Tree.Nodes': NODE_ITEMS },
    },
    required: false,
    searchable: false,
    ...overrides,
  };
}

describe('TreeViewComponent', () => {
  function render(fields: FlexFieldValue, value: unknown, showInList = false) {
    const fixture = TestBed.createComponent(TreeViewComponent);
    fixture.componentRef.setInput('fields', fields);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('showInList', showInList);
    fixture.detectChanges();
    return fixture;
  }

  it('shows the node title for a stored value, not the raw key', () => {
    const fixture = render(fieldValue(), 'apple');
    expect(fixture.nativeElement.textContent).toContain('Apple');
    expect(fixture.componentInstance.displayValue).not.toContain('apple');
  });

  it('joins the titles of every selected node for a multi-valued field', () => {
    const fixture = render(fieldValue(), ['apple', 'banana']);
    expect(fixture.componentInstance.displayValue).toBe('Apple, Banana');
  });

  it('falls back to the raw stored value when it matches no node', () => {
    const fixture = render(fieldValue(), 'unknown-key');
    expect(fixture.componentInstance.displayValue).toBe('unknown-key');
  });

  it('finds a nested node by key, not just top-level ones', () => {
    const fixture = render(fieldValue(), 'fruit');
    expect(fixture.componentInstance.displayValue).toBe('Fruit');
  });

  it('renders bare in list mode and inside a label wrapper otherwise', () => {
    expect(render(fieldValue(), 'apple', true).nativeElement.textContent).toContain('Apple');
    expect(render(fieldValue(), 'apple', false).nativeElement.textContent).toContain('Apple');
  });
});
