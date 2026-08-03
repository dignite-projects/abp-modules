using System;
using System.Collections.Generic;
using System.Linq;
using Dignite.Abp.FlexFields.Date;
using Dignite.Abp.FlexFields.Number;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Boolean;
using Dignite.Abp.FlexFields.Text;
using Dignite.Abp.FlexFields.Tree;
using Shouldly;
using Xunit;
using static Dignite.Abp.FlexFields.FlexFieldDataTestExtensions;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// <see cref="IFieldType.GetSearchableValues"/> across every built-in field type: FieldTypeBase's default
/// single-value decomposition, and the Select/Tree multi-value overrides. Typing the raw values into a
/// provider's storage slots (e.g. int -&gt; decimal) is deliberately out of scope here - that only happens
/// downstream, in the EF Core provider's <c>FlexFieldIndexValue.Create</c>.
/// </summary>
public class FieldTypeSearchableValues_Tests : DigniteAbpFlexFieldsTestBase
{
    private readonly IFieldTypeResolver _resolver;

    public FieldTypeSearchableValues_Tests()
    {
        _resolver = GetRequiredService<IFieldTypeResolver>();
    }

    [Fact]
    public void Text_field_yields_a_single_string_value()
    {
        var fieldType = _resolver.Get(TextFieldType.ControlName);
        var field = Create("Title", TextFieldType.ControlName).ToValue("Hello");

        var value = fieldType.GetSearchableValues(field).ShouldHaveSingleItem();

        value.ShouldBe("Hello");
    }

    [Fact]
    public void Number_field_yields_a_single_raw_value_untyped()
    {
        var fieldType = _resolver.Get(NumberFieldType.ControlName);
        var field = Create("ViewCount", NumberFieldType.ControlName).ToValue(42);

        var value = fieldType.GetSearchableValues(field).ShouldHaveSingleItem();

        // Still the raw int, not yet a decimal - that conversion is the EF Core provider's job.
        value.ShouldBe(42);
    }

    [Fact]
    public void DateTime_field_yields_a_single_date_value()
    {
        var fieldType = _resolver.Get(DateTimeFieldType.ControlName);
        var dateValue = new DateTime(2025, 1, 1);
        var field = Create("PublishedAt", DateTimeFieldType.ControlName).ToValue(dateValue);

        var value = fieldType.GetSearchableValues(field).ShouldHaveSingleItem();

        value.ShouldBe(dateValue);
    }

    [Fact]
    public void Boolean_field_yields_a_single_bool_value()
    {
        var fieldType = _resolver.Get(BooleanFieldType.ControlName);
        var field = Create("IsFeatured", BooleanFieldType.ControlName).ToValue(true);

        var value = fieldType.GetSearchableValues(field).ShouldHaveSingleItem();

        value.ShouldBe(true);
    }

    [Fact]
    public void Select_field_yields_one_value_per_selected_option()
    {
        var fieldType = _resolver.Get(SelectFieldType.ControlName);
        var field = Create("Tags", SelectFieldType.ControlName)
            .ToValue(new List<string> { "red", "blue" });

        var values = fieldType.GetSearchableValues(field).ToList();

        values.ShouldBe(new object[] { "red", "blue" });
    }

    [Fact]
    public void Tree_field_yields_one_value_per_selected_node()
    {
        var fieldType = _resolver.Get(TreeFieldType.ControlName);
        var field = Create("Categories", TreeFieldType.ControlName)
            .ToValue(new List<string> { "node-1", "node-2" });

        var values = fieldType.GetSearchableValues(field).ToList();

        values.ShouldBe(new object[] { "node-1", "node-2" });
    }

    [Fact]
    public void Non_searchable_usage_yields_nothing()
    {
        var fieldType = _resolver.Get(TextFieldType.ControlName);
        var field = Create("Title", TextFieldType.ControlName).ToValue("Hello", searchable: false);

        fieldType.GetSearchableValues(field).ShouldBeEmpty();
    }

    [Fact]
    public void Null_value_yields_nothing()
    {
        var fieldType = _resolver.Get(TextFieldType.ControlName);
        var field = Create("Title", TextFieldType.ControlName).ToValue(null);

        fieldType.GetSearchableValues(field).ShouldBeEmpty();
    }
}
