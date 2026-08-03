namespace Dignite.Abp.FlexFields.Number;

public class NumberConfiguration : FieldConfigurationBase
{
    /// <summary>
    /// Maximum number of decimal places after the decimal separator.
    /// </summary>
    public int Decimals {
        get => ConfigurationDictionary.GetConfiguration<int>(NumberConfigurationNames.Decimals, 2);
        set => ConfigurationDictionary.SetConfiguration(NumberConfigurationNames.Decimals, value);
    }

    public decimal? Max {
        get => ConfigurationDictionary.GetConfiguration<decimal?>(NumberConfigurationNames.Max);
        set => ConfigurationDictionary.SetConfiguration(NumberConfigurationNames.Max, value);
    }

    public decimal? Min {
        get => ConfigurationDictionary.GetConfiguration<decimal?>(NumberConfigurationNames.Min);
        set => ConfigurationDictionary.SetConfiguration(NumberConfigurationNames.Min, value);
    }

    /// <summary>
    /// Specifies the interval between valid values.
    /// </summary>
    public decimal? Step {
        get => ConfigurationDictionary.GetConfiguration<decimal?>(NumberConfigurationNames.Step);
        set => ConfigurationDictionary.SetConfiguration(NumberConfigurationNames.Step, value);
    }

    /// <summary>
    /// Format Specifier
    /// </summary>
    public string? FormatSpecifier {
        get => ConfigurationDictionary.GetConfiguration<string?>(NumberConfigurationNames.FormatSpecifier, null);
        set => ConfigurationDictionary.SetConfiguration(NumberConfigurationNames.FormatSpecifier, value);
    }

    public NumberConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public NumberConfiguration() : base()
    {
    }
}
