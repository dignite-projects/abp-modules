import { FormGroup } from '@angular/forms';
import { TestBed } from '@angular/core/testing';
import { NzFormatBeforeDropEvent } from 'ng-zorro-antd/tree';
import { CoreTestingModule } from '@abp/ng.core/testing';
import { NgxValidateCoreModule } from '@ngx-validate/core';
import { FlexFieldData } from '../../models';
import { TreeConfigComponent } from './tree-config.component';
import { checkedKeys } from './tree-node';

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

function fieldData(overrides: Partial<FlexFieldData> = {}): FlexFieldData {
  return {
    id: '1',
    name: 'category',
    displayName: 'Category',
    fieldTypeName: 'Tree',
    configuration: {},
    ...overrides,
  };
}

describe('TreeConfigComponent', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoreTestingModule.withConfig(), NgxValidateCoreModule.forRoot()],
    });
  });

  function render(selected?: FlexFieldData) {
    // The real template pulls in abp-modal (ng-bootstrap) and abp-tree, both heavy and irrelevant to
    // what's under test here - every test below drives the component's own methods directly, never
    // the rendered DOM, so an empty template avoids that machinery entirely.
    TestBed.overrideComponent(TreeConfigComponent, { set: { template: '', imports: [] } });
    const entity = new FormGroup({});
    const fixture = TestBed.createComponent(TreeConfigComponent);
    fixture.componentRef.setInput('type', 'Tree');
    fixture.componentRef.setInput('Entity', entity);
    if (selected) {
      fixture.componentRef.setInput('selected', selected);
    }
    fixture.detectChanges();
    return { fixture, entity, component: fixture.componentInstance };
  }

  it('starts a new field with an empty, required node list', () => {
    const { component, entity } = render();

    expect(component.nodes).toEqual([]);
    expect(entity.get(['configuration', 'Tree.Nodes'])).toBeTruthy();
    expect(component.treeNodesControl.hasError('required')).toBe(true);
  });

  it('loads the stored node tree when editing a field of this type', () => {
    const { component } = render(fieldData({ configuration: { 'Tree.Nodes': NODE_ITEMS } }));

    expect(component.nodes).toHaveLength(1);
    expect(component.nodes[0].title).toBe('Fruit');
    expect(component.nodes[0].children.map(child => child.key)).toEqual(['apple', 'banana']);
    expect(component.treeNodesControl.hasError('required')).toBe(false);
  });

  describe('modal', () => {
    it('opens a blank form for a new root node', () => {
      const { component } = render(fieldData({ configuration: { 'Tree.Nodes': NODE_ITEMS } }));

      component.addNode();

      expect(component.isModalVisible).toBe(true);
      expect(component.isCreatingChild).toBe(false);
      expect(component.editingNode).toBeNull();
      expect(component.nodeForm!.value).toEqual({ title: '', key: '', isChecked: false });
    });

    it('opens a pre-filled form for an existing node', () => {
      const { component } = render(fieldData({ configuration: { 'Tree.Nodes': NODE_ITEMS } }));
      const appleNode = component.nodes[0].children[0];

      component.editNode({ origin: appleNode });

      expect(component.editingNode).toBe(appleNode);
      expect(component.nodeForm!.value).toEqual({ title: 'Apple', key: 'apple', isChecked: false });
    });

    it('opens a blank form flagged as a child add', () => {
      const { component } = render(fieldData({ configuration: { 'Tree.Nodes': NODE_ITEMS } }));
      const fruitNode = component.nodes[0];

      component.addChildNode({ origin: fruitNode });

      expect(component.isCreatingChild).toBe(true);
      expect(component.editingNode).toBe(fruitNode);
      expect(component.nodeForm!.value.title).toBe('');
    });
  });

  describe('save', () => {
    it('does nothing when the node form is invalid', () => {
      const { component } = render();
      component.addNode();

      component.save();

      expect(component.nodes).toEqual([]);
      expect(component.isModalVisible).toBe(true);
    });

    it('adds a new root node and writes it into the stored configuration', () => {
      const { component, entity } = render();
      component.addNode();
      component.nodeForm!.patchValue({ title: 'Cherry', key: 'cherry' });

      component.save();

      expect(component.nodes.map(node => node.key)).toEqual(['cherry']);
      expect(component.isModalVisible).toBe(false);
      expect(entity.get(['configuration', 'Tree.Nodes'])!.value).toEqual([
        { Text: 'Cherry', Value: 'cherry', Selected: false, Children: [] },
      ]);
    });

    it('appends a new child under the node being edited, and expands it', () => {
      const { component } = render(fieldData({ configuration: { 'Tree.Nodes': NODE_ITEMS } }));
      const fruitNode = component.nodes[0];
      component.addChildNode({ origin: fruitNode });
      component.nodeForm!.patchValue({ title: 'Cherry', key: 'cherry' });

      component.save();

      expect(component.nodes[0].children.map(child => child.key)).toEqual(['apple', 'banana', 'cherry']);
      expect(component.expandedKeys).toContain('fruit');
    });

    it('edits an existing node in place rather than duplicating it', () => {
      const { component } = render(fieldData({ configuration: { 'Tree.Nodes': NODE_ITEMS } }));
      const appleNode = component.nodes[0].children[0];
      component.editNode({ origin: appleNode });
      component.nodeForm!.patchValue({ title: 'Green Apple', key: 'apple' });

      component.save();

      expect(component.nodes[0].children).toHaveLength(2);
      expect(component.nodes[0].children[0].title).toBe('Green Apple');
    });

    it('checking a node in single mode clears every other check', () => {
      const { component } = render(fieldData({ configuration: { 'Tree.Nodes': NODE_ITEMS } }));
      const appleNode = component.nodes[0].children[0];
      component.editNode({ origin: appleNode });
      component.nodeForm!.patchValue({ isChecked: true });
      component.save();

      const bananaNode = component.nodes[0].children[1];
      component.editNode({ origin: bananaNode });
      component.nodeForm!.patchValue({ isChecked: true });
      component.save();

      expect(checkedKeys(component.nodes)).toEqual(['banana']);
    });
  });

  it('removes a node and its subtree', () => {
    const { component } = render(fieldData({ configuration: { 'Tree.Nodes': NODE_ITEMS } }));

    component.deleteNode({ key: 'fruit' });

    expect(component.nodes).toEqual([]);
  });

  describe('toggleChecked', () => {
    it('exclusively checks the clicked node in single mode', () => {
      const { component } = render(fieldData({ configuration: { 'Tree.Nodes': NODE_ITEMS } }));

      component.toggleChecked(new Event('click'), { key: 'apple', origin: component.nodes[0].children[0] });
      component.toggleChecked(new Event('click'), { key: 'banana', origin: component.nodes[0].children[1] });

      expect(checkedKeys(component.nodes)).toEqual(['banana']);
    });

    it('checks ancestors too when checking a child in multiple mode', () => {
      const { component } = render(
        fieldData({ configuration: { 'Tree.Multiple': true, 'Tree.Nodes': NODE_ITEMS } }),
      );

      component.toggleChecked(new Event('click'), { key: 'apple', origin: component.nodes[0].children[0] });

      expect(checkedKeys(component.nodes).sort()).toEqual(['apple', 'fruit']);
    });
  });

  it('marks a parent indeterminate when a descendant is checked but the parent itself is not', () => {
    const partiallyChecked = [
      {
        Text: 'Fruit', Value: 'fruit', Selected: false,
        Children: [
          { Text: 'Apple', Value: 'apple', Selected: true, Children: [] },
          { Text: 'Banana', Value: 'banana', Selected: false, Children: [] },
        ],
      },
    ];
    const { component } = render(
      fieldData({ configuration: { 'Tree.Multiple': true, 'Tree.Nodes': partiallyChecked } }),
    );

    expect(component.isIndeterminate({ key: 'fruit', origin: component.nodes[0] })).toBe(true);
  });

  it('collapses to a single checked node when switching from multiple back to single', () => {
    const multiSelected = [
      {
        Text: 'Fruit', Value: 'fruit', Selected: false,
        Children: [
          { Text: 'Apple', Value: 'apple', Selected: true, Children: [] },
          { Text: 'Banana', Value: 'banana', Selected: true, Children: [] },
        ],
      },
    ];
    const { component, entity } = render(
      fieldData({ configuration: { 'Tree.Multiple': true, 'Tree.Nodes': multiSelected } }),
    );
    expect(checkedKeys(component.nodes)).toEqual(['apple', 'banana']);

    entity.get(['configuration', 'Tree.Multiple'])!.setValue(false);
    component.onMultipleChange();

    expect(checkedKeys(component.nodes)).toEqual(['apple']);
  });

  describe('onTitleBlur', () => {
    it('suggests a slugified key from the title when the key is still empty', () => {
      const { component } = render();
      component.addNode();

      component.onTitleBlur({ target: { value: 'New Fruit!' } } as unknown as Event);

      expect(component.keyInput.value).toBe('new-fruit');
    });

    it('never overwrites a key the user already typed', () => {
      const { component } = render();
      component.addNode();
      component.keyInput.setValue('custom-key');

      component.onTitleBlur({ target: { value: 'New Fruit' } } as unknown as Event);

      expect(component.keyInput.value).toBe('custom-key');
    });
  });

  describe('key validation', () => {
    it('rejects characters outside letters, digits, underscore and hyphen', () => {
      const { component } = render();
      component.addNode();

      component.keyInput.setValue('bad key!');

      expect(component.keyInput.errors?.['repetition']).toBeTruthy();
    });

    it('rejects a key that already exists elsewhere in the tree', () => {
      const { component } = render(fieldData({ configuration: { 'Tree.Nodes': NODE_ITEMS } }));
      component.addNode();

      component.keyInput.setValue('apple');

      expect(component.keyInput.errors?.['repetition']).toBeTruthy();
    });

    it('allows editing a node to keep its own existing key', () => {
      const { component } = render(fieldData({ configuration: { 'Tree.Nodes': NODE_ITEMS } }));
      const appleNode = component.nodes[0].children[0];

      component.editNode({ origin: appleNode });
      component.keyInput.setValue('apple');

      expect(component.keyInput.errors).toBeNull();
    });

    it('treats an empty key as valid on its own - required is a separate validator', () => {
      const { component } = render();
      component.addNode();

      component.keyInput.setValue('');

      expect(component.keyInput.errors?.['repetition']).toBeFalsy();
    });
  });

  describe('beforeDrop', () => {
    function flatNodes() {
      return [
        { title: 'A', key: 'a', entity: { key: 'a' }, isChecked: false, children: [] },
        { title: 'B', key: 'b', entity: { key: 'b' }, isChecked: false, children: [] },
        { title: 'C', key: 'c', entity: { key: 'c' }, isChecked: false, children: [] },
      ];
    }

    it('drops a node inside the target as its last child', () => {
      const { component } = render();
      component.nodes = flatNodes();

      let result: boolean | undefined;
      component
        .beforeDrop({ pos: 0, dragNode: { key: 'c' }, node: { key: 'a', parentNode: undefined } } as unknown as NzFormatBeforeDropEvent)
        .subscribe(value => (result = value));

      expect(result).toBe(false);
      expect(component.nodes.map(node => node.key)).toEqual(['a', 'b']);
      expect(component.nodes[0].children.map(node => node.key)).toEqual(['c']);
    });

    it('drops a node after the target as a sibling', () => {
      const { component } = render();
      component.nodes = flatNodes();

      component
        .beforeDrop({ pos: 1, dragNode: { key: 'a' }, node: { key: 'b', parentNode: undefined } } as unknown as NzFormatBeforeDropEvent)
        .subscribe();

      expect(component.nodes.map(node => node.key)).toEqual(['b', 'a', 'c']);
    });

    it('refuses to drop a node onto its own descendant', () => {
      const { component } = render(fieldData({ configuration: { 'Tree.Nodes': NODE_ITEMS } }));
      const before = component.nodes[0].children.map(node => node.key);

      let result: boolean | undefined;
      component
        .beforeDrop({ pos: 0, dragNode: { key: 'fruit' }, node: { key: 'apple', parentNode: undefined } } as unknown as NzFormatBeforeDropEvent)
        .subscribe(value => (result = value));

      expect(result).toBe(false);
      expect(component.nodes).toHaveLength(1);
      expect(component.nodes[0].children.map(node => node.key)).toEqual(before);
    });
  });
});
