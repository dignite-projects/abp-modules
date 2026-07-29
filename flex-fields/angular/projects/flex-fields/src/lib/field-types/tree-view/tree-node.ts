import { TreeViewNodeItem, normalizeTreeViewNodeItems } from './tree-view-configuration';

/**
 * A node in the shape `abp-tree` / `nz-tree-select` want. The stored form is
 * {@link TreeViewNodeItem}; this is the view model, and the two are converted at the edges rather
 * than each component reading whichever shape it happens to have.
 */
export interface TreeNode {
  title: string;
  key: string;
  isChecked: boolean;
  isLeaf?: boolean;
  expanded?: boolean;
  children: TreeNode[];
}

/** Stored options → view-model nodes. */
export function toTreeNodes(nodes: unknown): TreeNode[] {
  return normalizeTreeViewNodeItems(nodes).map(toTreeNode);
}

function toTreeNode(item: TreeViewNodeItem): TreeNode {
  const children = item.Children.map(toTreeNode);

  return {
    title: item.Text,
    key: item.Value,
    isChecked: item.Selected,
    isLeaf: children.length === 0,
    children,
  };
}

/** View-model nodes → stored options. */
export function toNodeItems(nodes: TreeNode[]): TreeViewNodeItem[] {
  return nodes.map(node => ({
    Text: node.title,
    Value: node.key,
    Selected: node.isChecked,
    Children: toNodeItems(node.children ?? []),
  }));
}

/** Depth-first walk over a node tree, parents before children. */
export function walkTreeNodes(nodes: TreeNode[], visit: (node: TreeNode) => void): void {
  for (const node of nodes) {
    visit(node);
    walkTreeNodes(node.children ?? [], visit);
  }
}

export function findTreeNode(nodes: TreeNode[], key: string): TreeNode | undefined {
  for (const node of nodes) {
    if (node.key === key) {
      return node;
    }

    const found = findTreeNode(node.children ?? [], key);
    if (found) {
      return found;
    }
  }

  return undefined;
}

/** Keys of every checked node, in depth-first order. */
export function checkedKeys(nodes: TreeNode[]): string[] {
  const keys: string[] = [];
  walkTreeNodes(nodes, node => {
    if (node.isChecked) {
      keys.push(node.key);
    }
  });

  return keys;
}

/** Keys of every node that has children — i.e. every node that is expandable. */
export function expandableKeys(nodes: TreeNode[]): string[] {
  const keys: string[] = [];
  walkTreeNodes(nodes, node => {
    if (node.children?.length) {
      keys.push(node.key);
    }
  });

  return keys;
}

export function clearChecked(nodes: TreeNode[]): void {
  walkTreeNodes(nodes, node => {
    node.isChecked = false;
  });
}

/** Checks or unchecks one node and everything under it. */
export function setCheckedCascading(nodes: TreeNode[], key: string, checked: boolean): void {
  const target = findTreeNode(nodes, key);

  if (target) {
    target.isChecked = checked;
    walkTreeNodes(target.children ?? [], child => {
      child.isChecked = checked;
    });
  }
}

/** Checks every ancestor of a node, so a checked leaf is never orphaned under unchecked parents. */
export function checkAncestors(nodes: TreeNode[], key: string): void {
  const path = pathTo(nodes, key);
  // The last entry is the node itself; everything before it is an ancestor.
  path.slice(0, -1).forEach(node => {
    node.isChecked = true;
  });
}

/** Removes a node, and returns whether it was found. */
export function removeTreeNode(nodes: TreeNode[], key: string): boolean {
  const index = nodes.findIndex(node => node.key === key);

  if (index >= 0) {
    nodes.splice(index, 1);
    return true;
  }

  return nodes.some(node => removeTreeNode(node.children ?? [], key));
}

/** Whether a key is already used anywhere in the tree, optionally ignoring one node. */
export function keyExists(nodes: TreeNode[], key: string, excludeKey?: string): boolean {
  let exists = false;
  walkTreeNodes(nodes, node => {
    if (node.key === key && node.key !== excludeKey) {
      exists = true;
    }
  });

  return exists;
}

function pathTo(nodes: TreeNode[], key: string, trail: TreeNode[] = []): TreeNode[] {
  for (const node of nodes) {
    const next = [...trail, node];

    if (node.key === key) {
      return next;
    }

    const found = pathTo(node.children ?? [], key, next);
    if (found.length) {
      return found;
    }
  }

  return [];
}
