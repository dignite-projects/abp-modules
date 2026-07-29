import { FormArray } from '@angular/forms';

/**
 * One node of a `TreeView` field's option tree. Mirrors `TreeViewNodeItem` on the server.
 *
 * Pascal-cased for the same reason as `SelectListItem`: these are the keys the designer writes, while
 * anything read back from the server arrives camel-cased.
 */
export interface TreeViewNodeItem {
  Text: string;
  Value: string;
  Selected: boolean;
  Children: TreeViewNodeItem[];
}

/** A stored node in either casing, as it may arrive from the designer or from the server. */
type RawTreeViewNodeItem = Partial<
  TreeViewNodeItem & { text: string; value: string; selected: boolean; children: unknown }
>;

/** Reads a stored `TreeView.Nodes` value, whatever casing it arrived in, recursively. */
export function normalizeTreeViewNodeItems(nodes: unknown): TreeViewNodeItem[] {
  if (!Array.isArray(nodes)) {
    return [];
  }

  return nodes.map((node: RawTreeViewNodeItem) => ({
    Text: node.Text ?? node.text ?? '',
    Value: node.Value ?? node.value ?? '',
    Selected: node.Selected ?? node.selected ?? false,
    Children: normalizeTreeViewNodeItems(node.Children ?? node.children),
  }));
}

/**
 * Configuration of a `TreeView` field, shaped for `FormBuilder.group()`. Mirrors
 * `TreeViewConfiguration` on the server.
 */
export class TreeViewConfiguration {
  'TreeView.Multiple': unknown = [false];

  'TreeView.Nodes': unknown = new FormArray<never>([]);
}
