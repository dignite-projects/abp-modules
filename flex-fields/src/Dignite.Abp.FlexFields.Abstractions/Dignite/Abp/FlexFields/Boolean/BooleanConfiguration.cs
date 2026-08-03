namespace Dignite.Abp.FlexFields.Boolean;

public class BooleanConfiguration : FieldConfigurationBase
{
    public bool Default {
        get => ConfigurationDictionary.GetConfiguration(BooleanConfigurationNames.Default, false);
        set => ConfigurationDictionary.SetConfiguration(BooleanConfigurationNames.Default, value);
    }

    public BooleanConfiguration() : base()
    {
    }

    public BooleanConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }
}
