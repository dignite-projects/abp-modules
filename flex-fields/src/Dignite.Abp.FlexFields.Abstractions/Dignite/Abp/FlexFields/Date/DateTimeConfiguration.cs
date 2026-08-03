using System;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.FlexFields.Date;

public class DateTimeConfiguration : FieldConfigurationBase
{
    [Required]
    public DateTimeInputMode InputMode {
        get => ConfigurationDictionary.GetConfiguration(DateTimeConfigurationNames.InputMode, DateTimeInputMode.Date);
        set => ConfigurationDictionary.SetConfiguration(DateTimeConfigurationNames.InputMode, value);
    }

    public DateTime? Max {
        get => ConfigurationDictionary.GetConfiguration<DateTime?>(DateTimeConfigurationNames.Max);
        set => ConfigurationDictionary.SetConfiguration(DateTimeConfigurationNames.Max, value);
    }

    public DateTime? Min {
        get => ConfigurationDictionary.GetConfiguration<DateTime?>(DateTimeConfigurationNames.Min);
        set => ConfigurationDictionary.SetConfiguration(DateTimeConfigurationNames.Min, value);
    }

    public DateTimeConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public DateTimeConfiguration() : base()
    {
    }
}
