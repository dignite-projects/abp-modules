using System;
using System.Collections.Generic;

namespace Dignite.Abp.FlexFields.Tree;

[Serializable]
public class TreeNodeItem
{
    public TreeNodeItem()
    {
    }

    public TreeNodeItem(string text, string value, bool selected)
    {
        Text = text;
        Value = value;
        Selected = selected;
    }

    public string Text { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public bool Selected { get; set; }

    public IList<TreeNodeItem> Children { get; set; } = new List<TreeNodeItem>();
}
