using System.Collections.Generic;
using Dignite.Abp.FlexFields.Tree;

namespace Dignite.Abp.FlexFields.Web;

/// <summary>Rendering-only helpers over <see cref="TreeConfiguration.Nodes"/> - not part of the kernel
/// because they exist purely to support the default view/search partials for the TreeView field type.</summary>
internal static class TreeNodeItemExtensions
{
    /// <summary>Depth-first labels of every node (recursively, including descendants) whose
    /// <see cref="TreeNodeItem.Value"/> is in <paramref name="selectedValues"/>.</summary>
    public static IEnumerable<string> FindSelectedLabels(this IEnumerable<TreeNodeItem> nodes, ICollection<string> selectedValues)
    {
        foreach (var node in nodes)
        {
            if (selectedValues.Contains(node.Value))
            {
                yield return node.Text;
            }

            foreach (var label in node.Children.FindSelectedLabels(selectedValues))
            {
                yield return label;
            }
        }
    }

    /// <summary>Depth-first flattening of the whole tree, pairing each node with its nesting depth
    /// (root = 0) - enough for a plain <c>&lt;select&gt;</c> to fake indentation without needing a real
    /// tree-picker widget.</summary>
    public static IEnumerable<(TreeNodeItem Node, int Depth)> Flatten(this IEnumerable<TreeNodeItem> nodes, int depth = 0)
    {
        foreach (var node in nodes)
        {
            yield return (node, depth);

            foreach (var descendant in node.Children.Flatten(depth + 1))
            {
                yield return descendant;
            }
        }
    }
}
