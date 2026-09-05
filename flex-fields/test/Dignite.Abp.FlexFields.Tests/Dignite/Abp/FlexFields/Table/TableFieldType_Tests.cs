using System;
using System.Collections.Generic;
using System.Text.Json;
using Dignite.Abp.FlexFields.Text;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.Table;

/// <summary>
/// <see cref="TableFieldType"/>'s normalization and recursive validation - see
/// <c>MatrixFieldType_Tests</c>, whose reasoning this mirrors exactly (a <see cref="TableRow"/> has only
/// <c>Values</c> and no type tag, so there is no "undeclared block type" case here).
/// </summary>
public class TableFieldType_Tests : DigniteAbpFlexFieldsTestBase
{
    private readonly TableFieldType _fieldType;

    public TableFieldType_Tests()
    {
        _fieldType = GetRequiredService<TableFieldType>();
    }

    [Fact]
    public void Should_Normalize_The_Row_Wrappers_Casing_To_CamelCase()
    {
        var element = JsonDocument.Parse("""[{"Values":{"text":"Hello"}}]""").RootElement.Clone();

        var normalized = (JsonElement)_fieldType.Normalize(element)!;

        normalized[0].GetProperty("values").GetProperty("text").GetString().ShouldBe("Hello");
    }

    [Fact]
    public void Should_Leave_Null_Unchanged_When_Normalizing()
    {
        _fieldType.Normalize(null).ShouldBeNull();
    }

    [Fact]
    public void Should_Normalize_A_Non_Array_Value_To_An_Empty_Array()
    {
        var element = JsonDocument.Parse("\"not-an-array\"").RootElement.Clone();

        var normalized = (JsonElement)_fieldType.Normalize(element)!;

        normalized.ValueKind.ShouldBe(JsonValueKind.Array);
        normalized.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public void Should_Leave_A_Value_With_A_Malformed_Element_Unchanged_When_Normalizing()
    {
        var element = JsonDocument.Parse("""[{"Values":{}},"not-a-row"]""").RootElement.Clone();

        var normalized = _fieldType.Normalize(element);

        ((JsonElement)normalized!).GetRawText().ShouldBe(element.GetRawText());
    }

    [Fact]
    public void An_Empty_Required_Table_Is_Reported_Once()
    {
        var field = CreateField(SpecColumnsConfiguration()).ToValue(value: null, searchable: false, required: true);

        var error = _fieldType.Validate(new FieldValidationArgs(field)).ShouldHaveSingleItem();

        error.MemberNames.ShouldBe(new[] { field.Name });
        // Localization really resolved - not left as the raw key.
        error.ErrorMessage.ShouldNotBe("Validate:Required");
        error.ErrorMessage.ShouldContain(field.DisplayName);
    }

    [Fact]
    public void An_Empty_Optional_Table_Is_Fine()
    {
        var field = CreateField(SpecColumnsConfiguration()).ToValue(value: null, searchable: false);

        _fieldType.Validate(new FieldValidationArgs(field)).ShouldBeEmpty();
    }

    /// <summary>
    /// The recursion that matters: every column's own <see cref="IFieldType.Validate"/> runs against every
    /// row, and its message is wrapped with the row index and column label.
    /// </summary>
    [Fact]
    public void A_Rows_Missing_Required_Cell_Is_Reported_With_Its_Row_And_Column()
    {
        var rows = new List<TableRow> { new() { Values = new FlexFieldDictionary() } };
        var field = CreateField(SpecColumnsConfiguration()).ToValue(rows, searchable: false);

        var error = _fieldType.Validate(new FieldValidationArgs(field)).ShouldHaveSingleItem();

        error.MemberNames.ShouldBe(new[] { field.Name });
        error.ErrorMessage.ShouldNotBe("Validate:Table:RowError");
        // "{0}: row {1}, column '{2}': {3}" - the field label, the 1-based row index, the column's own
        // label, and the inner Required message the Text field type produced.
        error.ErrorMessage.ShouldContain(field.DisplayName);
        error.ErrorMessage.ShouldContain("1");
        error.ErrorMessage.ShouldContain("Spec Name");
    }

    /// <summary>
    /// A column bound to a type name nothing registers - reported as one more validation error rather than
    /// the <c>AbpException</c> <see cref="IFieldTypeResolver.Get"/> would throw.
    /// </summary>
    [Fact]
    public void A_Column_Bound_To_An_Unregistered_Field_Type_Is_Reported()
    {
        var configuration = new FieldConfigurationDictionary();
        _ = new TableConfiguration(configuration)
        {
            Columns = new List<InlineFieldDefinition>
            {
                new() { Name = "name", DisplayName = "Spec Name", FieldTypeName = "NoSuchFieldType" }
            }
        };

        var rows = new List<TableRow> { new() { Values = new FlexFieldDictionary() } };
        var field = CreateField(configuration).ToValue(rows, searchable: false);

        var error = _fieldType.Validate(new FieldValidationArgs(field)).ShouldHaveSingleItem();

        error.ErrorMessage.ShouldContain("NoSuchFieldType");
    }

    /// <summary>One required Text column, <c>name</c>.</summary>
    private static FieldConfigurationDictionary SpecColumnsConfiguration()
    {
        var configuration = new FieldConfigurationDictionary();

        _ = new TableConfiguration(configuration)
        {
            Columns = new List<InlineFieldDefinition>
            {
                new()
                {
                    Name = "name",
                    DisplayName = "Spec Name",
                    FieldTypeName = TextFieldType.ControlName,
                    Required = true
                }
            }
        };

        return configuration;
    }

    private static FlexFieldData CreateField(FieldConfigurationDictionary configuration)
    {
        return new FlexFieldData(
            Guid.NewGuid(),
            "specs",
            displayName: "Specifications",
            fieldTypeName: TableFieldType.ControlName,
            configuration: configuration);
    }
}
