using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;

namespace Dignite.Abp.FlexFields.Table;

/// <summary>
/// The <c>Table</c> field type: a repeatable, homogeneous grid - one fixed column schema
/// (<see cref="TableConfiguration.Columns"/>) shared by every row, unlike <c>Matrix</c>'s several named,
/// independently-schemed block types. The literal "editable spreadsheet" case DynamicForms' own
/// <c>TableFormControl</c> covers. Shares <see cref="InlineFieldDefinition"/> (the column/sub-field
/// shape) and <c>InlineFieldValidator</c> (the recursive validation) with <c>MatrixFieldType</c>.
///
/// <para>
/// <b>Not indexable</b>, for the same reason <c>Matrix</c> is not: the value is a list of composite row
/// objects, not a scalar or list of scalars.
/// </para>
/// </summary>
public class TableFieldType : FieldTypeBase, ICompositeFieldType, INormalizesValue
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    public const string ControlName = "Table";

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:Table"];

    public override FlexFieldValueType? IndexValueType => null;

    public IEnumerable<InlineFieldDefinition> GetInlineFields(FieldConfigurationDictionary configuration)
    {
        return new TableConfiguration(configuration).Columns;
    }

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var errors = new List<ValidationResult>();
        var rows = ReadRows(args.Field.Value);

        if (!rows.Any())
        {
            if (args.Field.Required)
            {
                errors.Add(
                    new ValidationResult(
                        L["Validate:Required", args.Field.DisplayName],
                        new[] { args.Field.Name }
                        ));
            }

            return errors;
        }

        var configuration = new TableConfiguration(args.Field.Configuration);
        var fieldTypes = LazyServiceProvider.LazyGetRequiredService<IFieldTypeResolver>().GetAll();

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var capturedRowIndex = rowIndex;

            InlineFieldValidator.AppendValidationErrors(
                errors,
                configuration.Columns,
                rows[rowIndex].Values,
                fieldTypes,
                unknownFieldTypeError: field => new ValidationResult(
                    L["Validate:Table:UnknownFieldType", args.Field.DisplayName, field.FieldTypeName],
                    new[] { args.Field.Name }),
                invalidFieldError: (field, message) => new ValidationResult(
                    L["Validate:Table:RowError", args.Field.DisplayName, capturedRowIndex + 1, field.DisplayName, message],
                    new[] { args.Field.Name }));
        }

        return errors;
    }

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary fieldConfiguration)
    {
        return new TableConfiguration(fieldConfiguration);
    }

    /// <summary>
    /// Re-cases a structurally valid value to the canonical camelCase shape - see
    /// <c>MatrixFieldType.Normalize</c> for why an unparseable value is returned unchanged rather than
    /// coerced to <c>[]</c>.
    /// </summary>
    public object? Normalize(object? value)
    {
        if (value == null || value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
        {
            return value;
        }

        try
        {
            return JsonSerializer.SerializeToElement(ReadRows(value), WebOptions);
        }
        catch (JsonException)
        {
            return value;
        }
    }

    /// <summary>
    /// Storage-shape-agnostic read - the same duality every other field type's lenient reader handles,
    /// see <c>MatrixFieldType.ReadBlocks</c>.
    /// </summary>
    private static List<TableRow> ReadRows(object? value)
    {
        switch (value)
        {
            case null:
                return new List<TableRow>();
            case List<TableRow> list:
                return list;
            case JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined }:
                return new List<TableRow>();
            case JsonElement { ValueKind: JsonValueKind.Array } element:
                return element.Deserialize<List<TableRow>>(WebOptions)
                       ?? new List<TableRow>();
            default:
                return new List<TableRow>();
        }
    }
}
