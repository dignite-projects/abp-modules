using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.FlexFields.Text;

public class TextFieldType : FieldTypeBase
{
    public const string ControlName = "Text";

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:Text"];

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.String;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var configuration = new TextConfiguration(args.Field.Configuration);
        var errors = new List<ValidationResult>();

        if (args.Field.Value != null && !args.Field.Value.ToString().IsNullOrWhiteSpace())
        {
            if (configuration.CharLimit < args.Field.Value.ToString().Length)
            {
                errors.Add(
                    new ValidationResult(
                        L["Validate:CharacterCountExceedsLimit", args.Field.DisplayName, configuration.CharLimit],
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
        return new TextConfiguration(fieldConfiguration);
    }
}
