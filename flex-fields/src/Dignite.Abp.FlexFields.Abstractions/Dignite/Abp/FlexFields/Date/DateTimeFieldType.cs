using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.FlexFields.Date;

public class DateTimeFieldType : FieldTypeBase
{
    public const string ControlName = "DateEdit";

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:DateTime"];

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.DateTime;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var configuration = new DateConfiguration(args.Field.Configuration);
        var errors = new List<ValidationResult>();

        if (args.Field.Value != null && !args.Field.Value.ToString().IsNullOrWhiteSpace())
        {
            DateTime value;
            if (!DateTime.TryParse(args.Field.Value.ToString(), out value))
            {
                errors.Add(
                    new ValidationResult(
                        L["Validate:NotDateType", args.Field.DisplayName],
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
        return new DateConfiguration(fieldConfiguration);
    }
}
