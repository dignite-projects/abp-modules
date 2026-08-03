import { Component, Input } from '@angular/core';
import { CoreModule } from '@abp/ng.core';
import { FlexFieldValue } from '../../models';
import { readStringList } from '../../utils';
import { findTreeNode, toTreeNodes } from './tree-node';

/**
 * Displays the value of a `TreeView` field read-only, showing each node's label rather than the
 * stored value.
 */
@Component({
  selector: 'ff-tree-view',
  templateUrl: './tree-view.component.html',
  imports: [CoreModule],
})
export class TreeViewComponent {
  /** Renders bare, without the label wrapper, for use inside a table cell. */
  @Input() showInList = false;

  @Input() fields?: FlexFieldValue;

  /** Registration key of the field type, e.g. `TreeView`. */
  @Input() type?: string;

  @Input() value: unknown = '';

  get displayValue(): string {
    const nodes = toTreeNodes(this.fields?.field.configuration['TreeView.Nodes']);

    return readStringList(this.value)
      .map(stored => findTreeNode(nodes, stored)?.title ?? stored)
      .join(', ');
  }
}
