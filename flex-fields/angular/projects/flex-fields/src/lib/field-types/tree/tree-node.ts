import { TreeViewNodeItem, normalizeTreeViewNodeItems } from './tree-view-configuration';

/**
 * A node in the shape `abp-tree` / `nz-tree-select` want. The stored form is
 * {@link TreeViewNodeItem}; this is the view model, and the two are converted at the edges rather
 * than each component reading whichever shape it happens to have.
 */
export interface TreeNode {
  title: string;
  key: string;
  entity: { key: string };
  isChecked: boolean;
  expanded?: boolean;
  children: TreeNode[];
}

export interface FlatTreeNode {
  key: string;
  label: string;
}

/** Stored options → view-model nodes. */
export function toTreeNodes(nodes: unknown): TreeNode[] {
  return normalizeTreeViewNodeItems(nodes).map(toTreeNode);
}

/** Flattens a tree for native select controls while preserving its hierarchy in the label. */
export function flattenTreeNodes(nodes: TreeNode[], depth = 0): FlatTreeNode[] {
  return nodes.flatMap(node => [
    { key: node.key, label: `${'— '.repeat(depth)}${node.title}` },
    ...flattenTreeNodes(node.children ?? [], depth + 1),
  ]);
}

function toTreeNode(item: TreeViewNodeItem): TreeNode {
  return {
    title: item.Text,
    key: item.Value,
    entity: { key: item.Value },
    isChecked: item.Selected,
    children: item.Children.map(toTreeNode),
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

/** Toggles one key in an expanded-keys list — added if absent, removed if present. */
export function toggleExpandedKey(expandedKeys: string[], key: string | undefined): string[] {
  if (!key) {
    return expandedKeys;
  }

  return expandedKeys.includes(key)
    ? expandedKeys.filter(expanded => expanded !== key)
    : [...expandedKeys, key];
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

/** Keys of every ancestor of a node, root-first, not including the node itself. */
export function ancestorKeys(nodes: TreeNode[], key: string): string[] {
  return pathTo(nodes, key)
    .slice(0, -1)
    .map(node => node.key);
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
