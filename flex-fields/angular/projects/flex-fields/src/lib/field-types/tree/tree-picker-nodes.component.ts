import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TreeModule } from '@abp/ng.components/tree';
import { TreeNode, findTreeNode } from './tree-node';

/**
 * Renders a `Tree` field's node tree as a checkbox picker. This is the shared rendering core
 * behind `TreeControlComponent`'s always-expanded inline picker and `TreeSearchComponent`'s
 * dropdown picker — it owns how a node's checked/indeterminate state is drawn and reports clicks
 * upward as a plain key; the host decides what picking a key *means* (replace a single value vs.
 * add to a multi-select set).
 */
@Component({
  selector: 'ff-tree-picker-nodes',
  templateUrl: './tree-picker-nodes.component.html',
  styleUrls: ['./tree-picker-nodes.component.scss'],
  imports: [CommonModule, TreeModule],
})
export class TreePickerNodesComponent {
  @Input() nodes: TreeNode[] = [];
  @Input() expandedKeys: string[] = [];
  @Input() selectedKeys: string[] = [];
  @Input() multiple = false;

  @Output() readonly nodeToggle = new EventEmitter<string>();
  @Output() readonly expandChange = new EventEmitter<{ node?: { key?: string } }>();

  /** Bound by reference into `abp-tree`, so it must close over `this` rather than rely on a call site. */
  isNodeSelected = (node: { key: string }): boolean => {
    return !this.multiple && this.selectedKeys.includes(node.key);
  };

  isNodeChecked(node: { key: string }): boolean {
    return this.selectedKeys.includes(node.key);
  }

  isIndeterminate(node: { key: string }): boolean {
    if (!this.multiple || this.isNodeChecked(node)) {
      return false;
    }

    const target = findTreeNode(this.nodes, node.key);

    return !!target?.children?.length && this.hasCheckedDescendant(target.children);
  }

  onNodeClick(event: Event, node: { key: string }): void {
    event.stopPropagation();
    this.nodeToggle.emit(node.key);
  }

  onAbpTreeNodeChange(node: { key?: string } | undefined): void {
    if (node?.key) {
      this.nodeToggle.emit(node.key);
    }
  }

  private hasCheckedDescendant(nodes: TreeNode[]): boolean {
    return nodes.some(
      node => this.selectedKeys.includes(node.key) || this.hasCheckedDescendant(node.children ?? []),
    );
  }
}
