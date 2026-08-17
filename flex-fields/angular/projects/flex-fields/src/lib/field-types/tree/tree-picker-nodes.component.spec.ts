import { TestBed } from '@angular/core/testing';
import { TreeNode } from './tree-node';
import { TreePickerNodesComponent } from './tree-picker-nodes.component';

const NODES: TreeNode[] = [
  {
    title: 'Fruit',
    key: 'fruit',
    entity: { key: 'fruit' },
    isChecked: false,
    children: [
      { title: 'Apple', key: 'apple', entity: { key: 'apple' }, isChecked: false, children: [] },
      { title: 'Banana', key: 'banana', entity: { key: 'banana' }, isChecked: false, children: [] },
    ],
  },
];

function create() {
  return TestBed.createComponent(TreePickerNodesComponent);
}

describe('TreePickerNodesComponent', () => {
  it('renders the abp-tree widget once nodes are provided', () => {
    const fixture = create();
    fixture.componentInstance.nodes = NODES;
    expect(() => fixture.detectChanges()).not.toThrow();
    expect(fixture.nativeElement.querySelector('abp-tree')).toBeTruthy();
  });

  it('renders nothing when there are no nodes', () => {
    const fixture = create();
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('abp-tree')).toBeFalsy();
  });

  it('reports the sole selected key as selected only in single mode', () => {
    const component = create().componentInstance;
    component.multiple = false;
    component.selectedKeys = ['apple'];

    expect(component.isNodeSelected({ key: 'apple' })).toBe(true);
    expect(component.isNodeSelected({ key: 'banana' })).toBe(false);
  });

  it('never reports a key as "selected" in multiple mode - that state is single-mode only', () => {
    const component = create().componentInstance;
    component.multiple = true;
    component.selectedKeys = ['apple'];

    expect(component.isNodeSelected({ key: 'apple' })).toBe(false);
  });

  it('reports a key in selectedKeys as checked regardless of mode', () => {
    const component = create().componentInstance;
    component.selectedKeys = ['apple'];

    expect(component.isNodeChecked({ key: 'apple' })).toBe(true);
    expect(component.isNodeChecked({ key: 'banana' })).toBe(false);
  });

  describe('isIndeterminate', () => {
    it('marks a parent indeterminate when only some descendants are checked', () => {
      const component = create().componentInstance;
      component.multiple = true;
      component.nodes = NODES;
      component.selectedKeys = ['apple'];

      expect(component.isIndeterminate({ key: 'fruit' })).toBe(true);
    });

    it('does not mark a parent indeterminate once it is itself checked', () => {
      const component = create().componentInstance;
      component.multiple = true;
      component.nodes = NODES;
      component.selectedKeys = ['fruit', 'apple'];

      expect(component.isIndeterminate({ key: 'fruit' })).toBe(false);
    });

    it('does not mark a leaf indeterminate', () => {
      const component = create().componentInstance;
      component.multiple = true;
      component.nodes = NODES;

      expect(component.isIndeterminate({ key: 'apple' })).toBe(false);
    });

    it('is always false in single mode', () => {
      const component = create().componentInstance;
      component.multiple = false;
      component.nodes = NODES;
      component.selectedKeys = ['apple'];

      expect(component.isIndeterminate({ key: 'fruit' })).toBe(false);
    });
  });

  it('emits the clicked key on nodeToggle', () => {
    const component = create().componentInstance;
    const emitted: string[] = [];
    component.nodeToggle.subscribe((key: string) => emitted.push(key));

    component.onNodeClick(new Event('click'), { key: 'apple' });

    expect(emitted).toEqual(['apple']);
  });

  it('emits the key from an abp-tree selection change, ignoring an empty one', () => {
    const component = create().componentInstance;
    const emitted: string[] = [];
    component.nodeToggle.subscribe((key: string) => emitted.push(key));

    component.onAbpTreeNodeChange({ key: 'apple' });
    component.onAbpTreeNodeChange(undefined);
    component.onAbpTreeNodeChange({});

    expect(emitted).toEqual(['apple']);
  });
});
