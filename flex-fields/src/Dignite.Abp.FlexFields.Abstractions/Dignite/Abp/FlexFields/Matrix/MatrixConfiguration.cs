using System.Collections.Generic;

namespace Dignite.Abp.FlexFields.Matrix;

public class MatrixConfiguration : FieldConfigurationBase
{
    /// <summary>
    /// The schema: every block type an instance of this field can contain. Reads/writes through
    /// <see cref="FieldConfigurationDictionaryExtensions.GetConfiguration{TConfiguration}"/>'s existing
    /// JSON round-trip fallback (the same mechanism <c>TreeConfiguration.Nodes</c> and
    /// <c>SelectConfiguration.Options</c> already rely on for collection-shaped configuration) - nothing
    /// custom to write here.
    /// </summary>
    public List<MatrixBlockType> BlockTypes {
        get => ConfigurationDictionary.GetConfiguration(MatrixConfigurationNames.BlockTypes, new List<MatrixBlockType>());
        set => ConfigurationDictionary.SetConfiguration(MatrixConfigurationNames.BlockTypes, value);
    }

    public MatrixConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public MatrixConfiguration() : base()
    {
    }
}
