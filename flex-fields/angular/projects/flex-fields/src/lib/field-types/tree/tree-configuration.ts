/**
 * One node of a `Tree` field's option tree. Mirrors `TreeNodeItem` on the server.
 *
 * Pascal-cased for the same reason as `SelectListItem`: these are the keys the designer writes, while
 * anything read back from the server arrives camel-cased.
 */
export interface TreeNodeItem {
  Text: string;
  Value: string;
  Selected: boolean;
  Children: TreeNodeItem[];
}

/** A stored node in either casing, as it may arrive from the designer or from the server. */
type RawTreeNodeItem = Partial<
  TreeNodeItem & { text: string; value: string; selected: boolean; children: unknown }
>;

/** Reads a stored `Tree.Nodes` value, whatever casing it arrived in, recursively. */
export function normalizeTreeNodeItems(nodes: unknown): TreeNodeItem[] {
  if (!Array.isArray(nodes)) {
    return [];
  }

  return nodes.map((node: RawTreeNodeItem) => ({
    Text: node.Text ?? node.text ?? '',
    Value: node.Value ?? node.value ?? '',
    Selected: node.Selected ?? node.selected ?? false,
    Children: normalizeTreeNodeItems(node.Children ?? node.children),
  }));
}

/**
 * Configuration of a `Tree` field, shaped for `FormBuilder.group()`. Mirrors
 * `TreeConfiguration` on the server.
 */
export class TreeConfiguration {
  'Tree.Multiple': unknown = [false];

  'Tree.Nodes': unknown = [[]];
}
