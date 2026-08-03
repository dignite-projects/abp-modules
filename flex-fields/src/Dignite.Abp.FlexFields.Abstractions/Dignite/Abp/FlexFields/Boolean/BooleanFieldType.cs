using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Dignite.Abp.FlexFields.Boolean;

public class BooleanFieldType : FieldTypeBase
{
    public const string ControlName = "Switch";

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:Boolean"];

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.Boolean;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        return Array.Empty<ValidationResult>();
    }

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary fieldConfiguration)
    {
        return new BooleanConfiguration(fieldConfiguration);
    }
}
