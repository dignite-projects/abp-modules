using System.Collections.Generic;

namespace Dignite.Abp.FlexFields.Table;

public class TableConfiguration : FieldConfigurationBase
{
    /// <summary>
    /// The one column schema every row shares - unlike <c>Matrix.BlockTypes</c>, there is only ever one
    /// of these per field. Reads/writes through
    /// <see cref="FieldConfigurationDictionaryExtensions.GetConfiguration{TConfiguration}"/>'s existing
    /// JSON round-trip fallback, same as <c>MatrixConfiguration.BlockTypes</c>.
    /// </summary>
    public List<InlineFieldDefinition> Columns {
        get => ConfigurationDictionary.GetConfiguration(TableConfigurationNames.Columns, new List<InlineFieldDefinition>());
        set => ConfigurationDictionary.SetConfiguration(TableConfigurationNames.Columns, value);
    }

    public TableConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public TableConfiguration() : base()
    {
    }
}
