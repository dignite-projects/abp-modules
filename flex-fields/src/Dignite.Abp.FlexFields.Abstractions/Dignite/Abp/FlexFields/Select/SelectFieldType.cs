using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Dignite.Abp.FlexFields.Select;

public class SelectFieldType : FieldTypeBase
{
    public const string ControlName = "Select";

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:Select"];

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.String;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var configuration = new SelectConfiguration(args.Field.Configuration);
        var errors = new List<ValidationResult>();
        var value = ReadStringList(args.Field.Value);

        if (args.Field.Value != null)
        {
            if (value.Except(configuration.Options.Select(x => x.Value)).Any())
            {
                errors.Add(
                    new ValidationResult(
                        L["Validate:InvalidSelection", args.Field.DisplayName],
                        new[] { args.Field.Name }
                        ));
            }
        }
        else
        {
            if (!value.Any() && args.Field.Required)
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
        return new SelectConfiguration(fieldConfiguration);
    }

    /// <summary>
    /// Multi-valued override: one searchable value per selected option.
    /// </summary>
    public override IEnumerable<object> GetSearchableValues(FlexFieldValue field)
    {
        if (!field.Searchable || field.Value == null)
        {
            yield break;
        }

        foreach (var value in ReadStringList(field.Value))
        {
            yield return value;
        }
    }
}
