using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.FlexFields.Date;

public class DateTimeFieldType : FieldTypeBase
{
    public const string ControlName = "DateTime";

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:DateTime"];

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.DateTime;

    private static readonly FieldConfigurationKey[] Keys =
    {
        new(DateTimeConfigurationNames.InputMode, FieldConfigurationValueKind.Enum, typeof(DateTimeInputMode))
        {
            DefaultValue = (int)new DateTimeConfiguration().InputMode,
            Description = "What the user picks: a date, a date and time, or a year and month."
        },
        new(DateTimeConfigurationNames.Min, FieldConfigurationValueKind.DateTime)
        {
            EmptyStringIsUnset = true,
            Description = "The earliest value accepted."
        },
        new(DateTimeConfigurationNames.Max, FieldConfigurationValueKind.DateTime)
        {
            EmptyStringIsUnset = true,
            Description = "The latest value accepted."
        }
    };

    public override IReadOnlyList<FieldConfigurationKey>? ConfigurationKeys => Keys;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var configuration = new DateTimeConfiguration(args.Field.Configuration);
        var errors = new List<ValidationResult>();

        if (args.Field.Value != null && !args.Field.Value.ToString().IsNullOrWhiteSpace())
        {
            DateTime value;
            if (!DateTime.TryParse(args.Field.Value.ToString(), out value))
            {
                errors.Add(
                    new ValidationResult(
                        L["Validate:NotDateTimeType", args.Field.DisplayName],
                        new[] { args.Field.Name }
                        ));
            }

            if (configuration.Max.HasValue && configuration.Max.Value < value)
            {
                errors.Add(
                    new ValidationResult(
                        L["Validate:CannotBeGreaterThan", args.Field.DisplayName, configuration.Max.Value],
                        new[] { args.Field.Name }
                        ));
            }

            if (configuration.Min.HasValue && configuration.Min.Value > value)
            {
                errors.Add(
                    new ValidationResult(
                        L["Validate:CannotBeLessThan", args.Field.DisplayName, configuration.Min.Value],
                        new[] { args.Field.Name }
                        ));
            }
        }
        else
        {
            if (args.Field.Required)
            {
                errors.Add(
                    new ValidationResult(
                        L["Validate:Required", args.Field.DisplayName],
                        new[] { args.Field.Name }
                        ));
            }
        }

        return errors;
    }

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary fieldConfiguration)
    {
        return new DateTimeConfiguration(fieldConfiguration);
    }
}
