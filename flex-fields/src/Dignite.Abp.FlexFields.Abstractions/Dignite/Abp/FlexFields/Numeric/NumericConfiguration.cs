namespace Dignite.Abp.FlexFields.Numeric;

public class NumericConfiguration : FieldConfigurationBase
{
    /// <summary>
    /// Maximum number of decimal places after the decimal separator.
    /// </summary>
    public int Decimals {
        get => ConfigurationDictionary.GetConfiguration<int>(NumericConfigurationNames.Decimals, 2);
        set => ConfigurationDictionary.SetConfiguration(NumericConfigurationNames.Decimals, value);
    }

    public decimal? Max {
        get => ConfigurationDictionary.GetConfiguration<decimal?>(NumericConfigurationNames.Max);
        set => ConfigurationDictionary.SetConfiguration(NumericConfigurationNames.Max, value);
    }

    public decimal? Min {
        get => ConfigurationDictionary.GetConfiguration<decimal?>(NumericConfigurationNames.Min);
        set => ConfigurationDictionary.SetConfiguration(NumericConfigurationNames.Min, value);
    }

    /// <summary>
    /// Specifies the interval between valid values.
    /// </summary>
    public decimal? Step {
        get => ConfigurationDictionary.GetConfiguration<decimal?>(NumericConfigurationNames.Step);
        set => ConfigurationDictionary.SetConfiguration(NumericConfigurationNames.Step, value);
    }

    /// <summary>
    /// Format Specifier
    /// </summary>
    public string? FormatSpecifier {
        get => ConfigurationDictionary.GetConfiguration<string?>(NumericConfigurationNames.FormatSpecifier, null);
        set => ConfigurationDictionary.SetConfiguration(NumericConfigurationNames.FormatSpecifier, value);
    }

    public NumericConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public NumericConfiguration() : base()
    {
    }
}
