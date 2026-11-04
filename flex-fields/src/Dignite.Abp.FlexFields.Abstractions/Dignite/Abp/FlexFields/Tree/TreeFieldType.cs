using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Dignite.Abp.FlexFields.Tree;

public class TreeFieldType : FieldTypeBase
{
    public const string ControlName = "Tree";

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:Tree"];

    public override FlexFieldValueType? IndexValueType => FlexFieldValueType.String;

    private static readonly FieldConfigurationKey[] Keys =
    {
        new(TreeConfigurationNames.Nodes, FieldConfigurationValueKind.Array, typeof(List<TreeNodeItem>))
        {
            Description = "The tree. A value of the field has to be the value of one of its nodes."
        },
        new(TreeConfigurationNames.Multiple, FieldConfigurationValueKind.Boolean)
        {
            DefaultValue = new TreeConfiguration().Multiple,
            Description = "Whether several nodes can be selected. The value is then a list of node values."
        }
    };

    public override IReadOnlyList<FieldConfigurationKey>? ConfigurationKeys => Keys;

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var configuration = new TreeConfiguration(args.Field.Configuration);
        var errors = new List<ValidationResult>();
        var value = ReadStringList(args.Field.Value);

        if (args.Field.Value != null)
        {
            foreach (var item in value)
            {
                if (!configuration.Nodes.ContainsValue(item))
                {
                    errors.Add(
                        new ValidationResult(
                            L["Validate:InvalidSelection", args.Field.DisplayName],
                            new[] { args.Field.Name }
                            ));
                    return errors;
                }
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
        return new TreeConfiguration(fieldConfiguration);
    }

    /// <summary>
    /// Multi-valued override: one searchable value per selected node.
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
