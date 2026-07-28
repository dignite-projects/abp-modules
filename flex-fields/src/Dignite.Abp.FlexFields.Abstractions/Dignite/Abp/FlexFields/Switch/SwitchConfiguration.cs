namespace Dignite.Abp.FlexFields.Switch;

public class SwitchConfiguration : FieldConfigurationBase
{
    public bool Default {
        get => ConfigurationDictionary.GetConfiguration(SwitchConfigurationNames.Default, false);
        set => ConfigurationDictionary.SetConfiguration(SwitchConfigurationNames.Default, value);
    }

    public SwitchConfiguration() : base()
    {
    }

    public SwitchConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }
}
