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

    private static readonly FieldConfigurationKey[] Keys =
    {
        new(TextConfigurationNames.Mode, FieldConfigurationValueKind.Enum, typeof(TextMode))
        {
            DefaultValue = (int)new TextConfiguration().Mode,
            Description = "Whether the input is a single line or a multi-line text area."
        },
        new(TextConfigurationNames.CharLimit, FieldConfigurationValueKind.Integer)
        {
            Description = $"The most characters a value may have. Defaults to {CharLimitOf(TextMode.SingleLine)} for a single-line field " +
                          $"and {CharLimitOf(TextMode.MultipleLine)} for a multi-line one."
        },
        new(TextConfigurationNames.Placeholder, FieldConfigurationValueKind.String)
        {
            Description = "The placeholder of the input."
        }
    };

    public override IReadOnlyList<FieldConfigurationKey>? ConfigurationKeys => Keys;

    // The configuration class owns the defaults; a declaration reads them from it rather than restating them.
    private static int CharLimitOf(TextMode mode) => new TextConfiguration { Mode = mode }.CharLimit;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var configuration = new TextConfiguration(args.Field.Configuration);
        var errors = new List<ValidationResult>();

        var textValue = args.Field.Value?.ToString();
        if (!textValue.IsNullOrWhiteSpace())
        {
            if (configuration.CharLimit < textValue.Length)
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
