using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.TreeView;
using Shouldly;
using Xunit;
using static Dignite.Abp.FlexFields.FlexFieldDataTestExtensions;

namespace Dignite.Abp.FlexFields;

/// <summary>
/// A multi-valued field type has to read its bag value whatever shape the round trip left it in. A value bag
/// is a <c>Dictionary&lt;string, object&gt;</c>, and what comes back out of one depends on who put it back:
/// an entity still in memory holds a <c>List&lt;string&gt;</c>, a JSON deserializer that does not infer types
/// leaves a <see cref="JsonElement"/>, and a document driver leaves a list of boxed elements. A field type
/// that only handles one of those works only on the storage it was written against, which is not something a
/// field type is supposed to know.
/// </summary>
public class FieldTypeValueShapes_Tests : DigniteAbpFlexFieldsTestBase
{
    private readonly IFieldTypeResolver _resolver;

    public FieldTypeValueShapes_Tests()
    {
        _resolver = GetRequiredService<IFieldTypeResolver>();
    }

    public static IEnumerable<object[]> MultiValueFieldTypes()
    {
        yield return new object[] { SelectFieldType.ControlName };
        yield return new object[] { TreeFieldType.ControlName };
    }

    [Theory]
    [MemberData(nameof(MultiValueFieldTypes))]
    public void A_native_string_list_is_read(string fieldTypeName)
    {
        SearchableValues(fieldTypeName, new List<string> { "red", "blue" })
            .ShouldBe(new object[] { "red", "blue" });
    }

    [Theory]
    [MemberData(nameof(MultiValueFieldTypes))]
    public void A_json_element_array_is_read(string fieldTypeName)
    {
        var element = JsonDocument.Parse("[\"red\",\"blue\"]").RootElement.Clone();

        SearchableValues(fieldTypeName, element).ShouldBe(new object[] { "red", "blue" });
    }

    [Theory]
    [MemberData(nameof(MultiValueFieldTypes))]
    public void A_list_of_boxed_elements_is_read(string fieldTypeName)
    {
        // The shape a document driver leaves behind: the bag's value type is object, so a BSON array comes
        // back as List<object>, not List<string>. Casting straight to List<string> throws here.
        SearchableValues(fieldTypeName, new List<object> { "red", "blue" })
            .ShouldBe(new object[] { "red", "blue" });
    }

    [Theory]
    [MemberData(nameof(MultiValueFieldTypes))]
    public void A_bare_string_reads_as_a_single_selection(string fieldTypeName)
    {
        SearchableValues(fieldTypeName, "red").ShouldBe(new object[] { "red" });
    }

    [Theory]
    [MemberData(nameof(MultiValueFieldTypes))]
    public void An_empty_list_yields_nothing(string fieldTypeName)
    {
        SearchableValues(fieldTypeName, new List<string>()).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(MultiValueFieldTypes))]
    public void A_null_value_yields_nothing(string fieldTypeName)
    {
        SearchableValues(fieldTypeName, null).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(MultiValueFieldTypes))]
    public void A_non_searchable_usage_yields_nothing_whatever_the_shape(string fieldTypeName)
    {
        var fieldType = _resolver.Get(fieldTypeName);
        var field = Create("Tags", fieldTypeName).ToValue(new List<object> { "red" }, searchable: false);

        fieldType.GetSearchableValues(field).ShouldBeEmpty();
    }

    private List<object> SearchableValues(string fieldTypeName, object? value)
    {
        var fieldType = _resolver.Get(fieldTypeName);
        var field = Create("Tags", fieldTypeName).ToValue(value);

        return fieldType.GetSearchableValues(field).ToList();
    }
}
