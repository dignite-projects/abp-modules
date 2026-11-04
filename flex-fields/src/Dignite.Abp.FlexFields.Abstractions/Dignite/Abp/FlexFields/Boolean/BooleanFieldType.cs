using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.FlexFields.Boolean;

public class BooleanFieldType : FieldTypeBase
{
    public const string ControlName = "Boolean";

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:Boolean"];

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.Boolean;

    private static readonly FieldConfigurationKey[] Keys =
    {
        new(BooleanConfigurationNames.Default, FieldConfigurationValueKind.Boolean)
        {
            DefaultValue = new BooleanConfiguration().Default,
            Description = "The initial value of a new entity."
        }
    };

    public override IReadOnlyList<FieldConfigurationKey>? ConfigurationKeys => Keys;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        return Array.Empty<ValidationResult>();
    }

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary fieldConfiguration)
    {
        return new BooleanConfiguration(fieldConfiguration);
    }
}
