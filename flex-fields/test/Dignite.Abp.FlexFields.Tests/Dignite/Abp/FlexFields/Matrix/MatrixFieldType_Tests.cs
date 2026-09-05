using System;
using System.Collections.Generic;
using System.Text.Json;
using Dignite.Abp.FlexFields.Text;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.Matrix;

/// <summary>
/// <see cref="MatrixFieldType"/>'s two halves that nothing else covers: <see cref="MatrixFieldType.Normalize"/>
/// (the outer block-array wrapper's own casing - not the admin-configured sub-field names inside
/// <see cref="MatrixBlockValue.Values"/>, which are a free-form bag <c>Normalize</c> deliberately leaves
/// alone) and the recursive validation, which is what makes this type more than a bag of JSON.
/// </summary>
public class MatrixFieldType_Tests : DigniteAbpFlexFieldsTestBase
{
    private readonly MatrixFieldType _fieldType;

    public MatrixFieldType_Tests()
    {
        _fieldType = GetRequiredService<MatrixFieldType>();
    }

    /// <summary>
    /// <see cref="MatrixBlockValue"/>'s own fixed properties (<c>BlockTypeName</c>, <c>Values</c>) must
    /// round-trip to camelCase regardless of how a caller cased them, even though the keys inside
    /// <c>Values</c> - the admin's own sub-field names - are untouched.
    /// </summary>
    [Fact]
    public void Should_Normalize_The_Block_Wrappers_Casing_To_CamelCase()
    {
        var element = JsonDocument.Parse(
            """[{"BlockTypeName":"quote","Values":{"text":"Hello"}}]""").RootElement.Clone();

        var normalized = (JsonElement)_fieldType.Normalize(element)!;

        normalized[0].GetProperty("blockTypeName").GetString().ShouldBe("quote");
        normalized[0].GetProperty("values").GetProperty("text").GetString().ShouldBe("Hello");
    }

    [Fact]
    public void Should_Leave_Null_Unchanged_When_Normalizing()
    {
        _fieldType.Normalize(null).ShouldBeNull();
    }

    /// <summary>
    /// A value that is not array-shaped at all normalizes to <c>[]</c>, same as <c>ReadBlocks</c> already
    /// resolves it for <see cref="MatrixFieldType.Validate"/> (the <c>default</c> case) - the same
    /// coalescing, applied one step earlier.
    /// </summary>
    [Fact]
    public void Should_Normalize_A_Non_Array_Value_To_An_Empty_Array()
    {
        var element = JsonDocument.Parse("\"not-an-array\"").RootElement.Clone();

        var normalized = (JsonElement)_fieldType.Normalize(element)!;

        normalized.ValueKind.ShouldBe(JsonValueKind.Array);
        normalized.GetArrayLength().ShouldBe(0);
    }

    /// <summary>
    /// Unlike a non-array value, an array with a structurally broken <i>element</i> (a bare string where a
    /// block object belongs) makes <c>System.Text.Json</c> throw while deserializing - which surfaces from
    /// <c>Validate</c>'s own call to <c>ReadBlocks</c>. Normalization must not turn that into a new,
    /// earlier failure point: it catches the same exception and returns the value unchanged, so the
    /// existing failure still happens at the same place it always did.
    /// </summary>
    [Fact]
    public void Should_Leave_A_Value_With_A_Malformed_Element_Unchanged_When_Normalizing()
    {
        var element = JsonDocument.Parse(
            """[{"BlockTypeName":"quote","Values":{}},"not-a-block"]""").RootElement.Clone();

        var normalized = _fieldType.Normalize(element);

        ((JsonElement)normalized!).GetRawText().ShouldBe(element.GetRawText());
    }

    [Fact]
    public void An_Empty_Required_Matrix_Is_Reported_Once()
    {
        var field = CreateField(QuoteBlockConfiguration()).ToValue(value: null, searchable: false, required: true);

        var error = _fieldType.Validate(new FieldValidationArgs(field)).ShouldHaveSingleItem();

        error.MemberNames.ShouldBe(new[] { field.Name });
        // Localization really resolved - not left as the raw key.
        error.ErrorMessage.ShouldNotBe("Validate:Required");
        error.ErrorMessage.ShouldContain(field.DisplayName);
    }

    [Fact]
    public void An_Empty_Optional_Matrix_Is_Fine()
    {
        var field = CreateField(QuoteBlockConfiguration()).ToValue(value: null, searchable: false);

        _fieldType.Validate(new FieldValidationArgs(field)).ShouldBeEmpty();
    }

    /// <summary>
    /// A block naming a block type the configuration no longer declares - what an admin deleting a block
    /// type leaves behind in already-stored values.
    /// </summary>
    [Fact]
    public void A_Block_Of_An_Undeclared_Block_Type_Is_Reported()
    {
        var blocks = new List<MatrixBlockValue>
        {
            new() { BlockTypeName = "gone", Values = new FlexFieldDictionary() }
        };
        var field = CreateField(QuoteBlockConfiguration()).ToValue(blocks, searchable: false);

        var error = _fieldType.Validate(new FieldValidationArgs(field)).ShouldHaveSingleItem();

        error.MemberNames.ShouldBe(new[] { field.Name });
        error.ErrorMessage.ShouldContain("gone");
    }

    /// <summary>
    /// The recursion that matters: a sub-field's own <see cref="IFieldType.Validate"/> runs against every
    /// block instance, and its message is wrapped with the block index and sub-field label so an admin can
    /// tell which of several blocks is at fault.
    /// </summary>
    [Fact]
    public void A_Blocks_Missing_Required_Sub_Field_Is_Reported_With_Its_Block_And_Field()
    {
        var blocks = new List<MatrixBlockValue>
        {
            new() { BlockTypeName = "quote", Values = new FlexFieldDictionary() }
        };
        var field = CreateField(QuoteBlockConfiguration()).ToValue(blocks, searchable: false);

        var error = _fieldType.Validate(new FieldValidationArgs(field)).ShouldHaveSingleItem();

        error.MemberNames.ShouldBe(new[] { field.Name });
        error.ErrorMessage.ShouldNotBe("Validate:Matrix:SubFieldError");
        // "{0}: block {1}, field '{2}': {3}" - the field label, the 1-based block index, the sub-field's
        // own label, and the inner Required message the Text field type produced.
        error.ErrorMessage.ShouldContain(field.DisplayName);
        error.ErrorMessage.ShouldContain("1");
        error.ErrorMessage.ShouldContain("Quote Text");
    }

    /// <summary>
    /// A sub-field bound to a type name nothing registers - what removing a bolt-on field type package
    /// leaves behind in an already-authored schema. Reported as one more validation error rather than the
    /// <c>AbpException</c> <see cref="IFieldTypeResolver.Get"/> would throw.
    /// </summary>
    [Fact]
    public void A_Sub_Field_Bound_To_An_Unregistered_Field_Type_Is_Reported()
    {
        var configuration = new FieldConfigurationDictionary();
        _ = new MatrixConfiguration(configuration)
        {
            BlockTypes = new List<MatrixBlockType>
            {
                new()
                {
                    Name = "quote",
                    DisplayName = "Quote",
                    Fields = new List<InlineFieldDefinition>
                    {
                        new() { Name = "text", DisplayName = "Quote Text", FieldTypeName = "NoSuchFieldType" }
                    }
                }
            }
        };

        var blocks = new List<MatrixBlockValue>
        {
            new() { BlockTypeName = "quote", Values = new FlexFieldDictionary() }
        };
        var field = CreateField(configuration).ToValue(blocks, searchable: false);

        var error = _fieldType.Validate(new FieldValidationArgs(field)).ShouldHaveSingleItem();

        error.ErrorMessage.ShouldContain("NoSuchFieldType");
    }

    /// <summary>One block type, <c>quote</c>, with one required Text sub-field.</summary>
    private static FieldConfigurationDictionary QuoteBlockConfiguration()
    {
        var configuration = new FieldConfigurationDictionary();

        _ = new MatrixConfiguration(configuration)
        {
            BlockTypes = new List<MatrixBlockType>
            {
                new()
                {
                    Name = "quote",
                    DisplayName = "Quote",
                    Fields = new List<InlineFieldDefinition>
                    {
                        new()
                        {
                            Name = "text",
                            DisplayName = "Quote Text",
                            FieldTypeName = TextFieldType.ControlName,
                            Required = true
                        }
                    }
                }
            }
        };

        return configuration;
    }

    private static FlexFieldData CreateField(FieldConfigurationDictionary configuration)
    {
        return new FlexFieldData(
            Guid.NewGuid(),
            "sections",
            displayName: "Content Sections",
            fieldTypeName: MatrixFieldType.ControlName,
            configuration: configuration);
    }
}
