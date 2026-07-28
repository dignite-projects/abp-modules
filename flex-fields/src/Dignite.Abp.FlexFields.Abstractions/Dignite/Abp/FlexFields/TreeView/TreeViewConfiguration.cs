using System.Collections.Generic;

namespace Dignite.Abp.FlexFields.TreeView;

public class TreeViewConfiguration : FieldConfigurationBase
{
    public bool Multiple {
        get => ConfigurationDictionary.GetConfiguration(TreeViewConfigurationNames.Multiple, false);
        set => ConfigurationDictionary.SetConfiguration(TreeViewConfigurationNames.Multiple, value);
    }
    public List<TreeViewNodeItem> Nodes {
        get => ConfigurationDictionary.GetConfiguration(TreeViewConfigurationNames.Nodes, new List<TreeViewNodeItem>());
        set => ConfigurationDictionary.SetConfiguration(TreeViewConfigurationNames.Nodes, value);
    }

    public TreeViewConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public TreeViewConfiguration() : base()
    {
    }
}
