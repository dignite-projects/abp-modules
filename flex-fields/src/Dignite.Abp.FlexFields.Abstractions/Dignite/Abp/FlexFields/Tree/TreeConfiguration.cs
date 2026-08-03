using System.Collections.Generic;

namespace Dignite.Abp.FlexFields.Tree;

public class TreeConfiguration : FieldConfigurationBase
{
    public bool Multiple {
        get => ConfigurationDictionary.GetConfiguration(TreeConfigurationNames.Multiple, false);
        set => ConfigurationDictionary.SetConfiguration(TreeConfigurationNames.Multiple, value);
    }
    public List<TreeNodeItem> Nodes {
        get => ConfigurationDictionary.GetConfiguration(TreeConfigurationNames.Nodes, new List<TreeNodeItem>());
        set => ConfigurationDictionary.SetConfiguration(TreeConfigurationNames.Nodes, value);
    }

    public TreeConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public TreeConfiguration() : base()
    {
    }
}
