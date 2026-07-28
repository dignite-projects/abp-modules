namespace Dignite.Abp.FlexFields;

public abstract class FieldConfigurationBase
{
    public FieldConfigurationDictionary ConfigurationDictionary { get; set; }

    public FieldConfigurationBase(
        FieldConfigurationDictionary fieldConfiguration)
    {
        ConfigurationDictionary = fieldConfiguration;
    }

    public FieldConfigurationBase()
    {
        ConfigurationDictionary = new FieldConfigurationDictionary();
    }
}
