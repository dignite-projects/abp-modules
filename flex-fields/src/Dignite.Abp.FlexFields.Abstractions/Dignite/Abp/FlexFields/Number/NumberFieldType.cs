using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.FlexFields.Number;

public class NumberFieldType : FieldTypeBase
{
    public const string ControlName = "Number";

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:Number"];

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.Number;

    private static readonly FieldConfigurationKey[] Keys =
    {
        new(NumberConfigurationNames.Min, FieldConfigurationValueKind.Number)
        {
            EmptyStringIsUnset = true,
            Description = "The smallest value accepted."
        },
        new(NumberConfigurationNames.Max, FieldConfigurationValueKind.Number)
        {
            EmptyStringIsUnset = true,
            Description = "The largest value accepted."
        },
        new(NumberConfigurationNames.Decimals, FieldConfigurationValueKind.Integer)
        {
            DefaultValue = new NumberConfiguration().Decimals,
            Description = "The most decimal places a value may have."
        },
        new(NumberConfigurationNames.Step, FieldConfigurationValueKind.Number)
        {
            EmptyStringIsUnset = true,
            Description = "The interval between valid values, for the input."
        },
        new(NumberConfigurationNames.FormatSpecifier, FieldConfigurationValueKind.String)
        {
            Description = "A .NET standard numeric format string, applied when the value is displayed. The key has no 'Number.' prefix."
        },
        new(NumberConfigurationNames.Placeholder, FieldConfigurationValueKind.String)
        {
            Description = "The placeholder of the input."
        }
    };

    public override IReadOnlyList<FieldConfigurationKey>? ConfigurationKeys => Keys;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var configuration = new NumberConfiguration(args.Field.Configuration);
        var errors = new List<ValidationResult>();

        if (args.Field.Value != null && !args.Field.Value.ToString().IsNullOrWhiteSpace())
        {
            decimal value;
            if (!decimal.TryParse(args.Field.Value.ToString(), out value))
            {
                errors.Add(
                    new ValidationResult(
                        L["Validate:NotNumberType", args.Field.DisplayName],
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
        return new NumberConfiguration(fieldConfiguration);
    }
}
