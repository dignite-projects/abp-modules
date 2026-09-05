using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;

namespace Dignite.Abp.FlexFields.Matrix;

/// <summary>
/// The <c>Matrix</c> field type: a repeatable list of polymorphic "blocks" - the admin declares one or
/// more named block types up front (<see cref="MatrixConfiguration.BlockTypes"/>), each with its own set
/// of sub-fields, and the value is a list of block instances, each an occurrence of one block type. Named
/// after (and semantically identical to) DynamicForms' own <c>MatrixFormControl</c> - the polymorphic
/// repeater, not the homogeneous one (<c>TableFieldType</c>, which shares <see cref="InlineFieldDefinition"/>
/// and <c>InlineFieldValidator</c> with this type but has only one shared column schema instead of
/// several named block types).
///
/// <para>
/// <b>Not indexable.</b> <see cref="IFieldType.IndexValueType"/>'s own doc comment names Matrix (with
/// RichText) as the canonical case that exists for: the value is a list of composite objects, not a
/// scalar or list of scalars, so there is no typed index column to decompose it into.
/// </para>
///
/// <para>
/// <b>Validates recursively, unlike DynamicForms' own <c>MatrixFormControl.Validate</c></b> (an empty
/// no-op there - nested Required/format rules have no server-side backstop in the reference
/// implementation). Every sub-field's own <see cref="IFieldType.Validate"/> runs against every block
/// instance that names it, resolved through <see cref="IFieldTypeResolver"/> - the same delegation
/// <c>FlexFieldValidator&lt;TEntity&gt;</c> does one level up, just invoked manually here (via
/// <c>InlineFieldValidator</c>) since the kernel's validation seam has no concept of a field whose value
/// contains other fields.
/// </para>
/// </summary>
public class MatrixFieldType : FieldTypeBase, ICompositeFieldType, INormalizesValue
{
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web);

    public const string ControlName = "Matrix";

    public override string Name => ControlName;

    public override string DisplayName => L["FieldType:Matrix"];

    public override FlexFieldValueType? IndexValueType => null;

    public IEnumerable<InlineFieldDefinition> GetInlineFields(FieldConfigurationDictionary configuration)
    {
        return new MatrixConfiguration(configuration).BlockTypes.SelectMany(blockType => blockType.Fields);
    }

    public override IReadOnlyList<ValidationResult> Validate(FieldValidationArgs args)
    {
        var errors = new List<ValidationResult>();
        var blocks = ReadBlocks(args.Field.Value);

        if (!blocks.Any())
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

        var configuration = new MatrixConfiguration(args.Field.Configuration);
        var fieldTypes = LazyServiceProvider.LazyGetRequiredService<IFieldTypeResolver>().GetAll();

        for (var blockIndex = 0; blockIndex < blocks.Count; blockIndex++)
        {
            var block = blocks[blockIndex];
            var blockType = configuration.BlockTypes.FirstOrDefault(bt => bt.Name == block.BlockTypeName);

            if (blockType == null)
            {
                errors.Add(
                    new ValidationResult(
                        L["Validate:Matrix:UnknownBlockType", args.Field.DisplayName, block.BlockTypeName],
                        new[] { args.Field.Name }
                        ));
                continue;
            }

            var capturedBlockIndex = blockIndex;

            // Attributed to this Matrix field's own Name, not a synthetic nested path - there is no
            // established convention for a nested member-name shape, and a form UI only ever has a
            // control for the Matrix field itself, so the block/sub-field context belongs in the message
            // rather than in MemberNames.
            InlineFieldValidator.AppendValidationErrors(
                errors,
                blockType.Fields,
                block.Values,
                fieldTypes,
                unknownFieldTypeError: field => new ValidationResult(
                    L["Validate:Matrix:UnknownFieldType", args.Field.DisplayName, field.FieldTypeName],
                    new[] { args.Field.Name }),
                invalidFieldError: (field, message) => new ValidationResult(
                    L["Validate:Matrix:SubFieldError", args.Field.DisplayName, capturedBlockIndex + 1, field.DisplayName, message],
                    new[] { args.Field.Name }));
        }

        return errors;
    }

    public override FieldConfigurationBase GetConfiguration(FieldConfigurationDictionary fieldConfiguration)
    {
        return new MatrixConfiguration(fieldConfiguration);
    }

    /// <summary>
    /// Re-cases a structurally valid value to the canonical camelCase shape (see
    /// <see cref="INormalizesValue"/>). Falls back to returning <paramref name="value"/> unchanged - rather
    /// than an empty list - if it does not even parse as JSON shaped like a block array, so a malformed
    /// value is left for <see cref="Validate"/> to report exactly as it does today; a value that parses but
    /// is semantically empty already normalizes to <c>[]</c> via <see cref="ReadBlocks"/> either way, which
    /// matches <see cref="Validate"/>'s own existing treatment of "empty" and "unparseable" as one case.
    /// </summary>
    public object? Normalize(object? value)
    {
        if (value == null || value is JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined })
        {
            return value;
        }

        try
        {
            return JsonSerializer.SerializeToElement(ReadBlocks(value), WebOptions);
        }
        catch (JsonException)
        {
            return value;
        }
    }

    /// <summary>
    /// Storage-shape-agnostic read: a fresh in-memory value is a live <see cref="List{MatrixBlockValue}"/>,
    /// one that has round-tripped through JSON storage is a <see cref="JsonElement"/> array - the same
    /// duality every other field type's lenient reader handles, just deserializing a composite element
    /// shape instead of a scalar one.
    /// </summary>
    private static List<MatrixBlockValue> ReadBlocks(object? value)
    {
        switch (value)
        {
            case null:
                return new List<MatrixBlockValue>();
            case List<MatrixBlockValue> list:
                return list;
            case JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined }:
                return new List<MatrixBlockValue>();
            case JsonElement { ValueKind: JsonValueKind.Array } element:
                return element.Deserialize<List<MatrixBlockValue>>(WebOptions)
                       ?? new List<MatrixBlockValue>();
            default:
                return new List<MatrixBlockValue>();
        }
    }
}
