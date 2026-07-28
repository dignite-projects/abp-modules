using System;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.FlexFields.Date;

public class DateConfiguration : FieldConfigurationBase
{
    [Required]
    public DateInputMode InputMode {
        get => ConfigurationDictionary.GetConfiguration(DateConfigurationNames.InputMode, DateInputMode.Date);
        set => ConfigurationDictionary.SetConfiguration(DateConfigurationNames.InputMode, value);
    }

    public DateTime? Max {
        get => ConfigurationDictionary.GetConfiguration<DateTime?>(DateConfigurationNames.Max);
        set => ConfigurationDictionary.SetConfiguration(DateConfigurationNames.Max, value);
    }

    public DateTime? Min {
        get => ConfigurationDictionary.GetConfiguration<DateTime?>(DateConfigurationNames.Min);
        set => ConfigurationDictionary.SetConfiguration(DateConfigurationNames.Min, value);
    }

    public DateConfiguration(FieldConfigurationDictionary fieldConfiguration)
        : base(fieldConfiguration)
    {
    }

    public DateConfiguration() : base()
    {
    }
}
