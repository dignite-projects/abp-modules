using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.FlexFields.Text;

public class TextConfiguration : FieldConfigurationBase
{
    public string? Placeholder {
        get => ConfigurationDictionary.GetConfiguration<string?>(TextConfigurationNames.Placeholder, null);
        set => ConfigurationDictionary.SetConfiguration(TextConfigurationNames.Placeholder, value);
    }

    [Required]
    public TextMode Mode {
        get => ConfigurationDictionary.GetConfiguration(TextConfigurationNames.Mode, TextMode.SingleLine);
        set => ConfigurationDictionary.SetConfiguration(TextConfigurationNames.Mode, value);
    }

    public int CharLimit {
        get => ConfigurationDictionary.GetConfiguration(TextConfigurationNames.CharLimit, Mode == TextMode.SingleLine ? 256 : 1024);
        set => ConfigurationDictionary.SetConfiguration(TextConfigurationNames.CharLimit, value);
    }

    public TextConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public TextConfiguration() : base()
    {
    }
}
