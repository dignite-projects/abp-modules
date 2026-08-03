using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Text;
using Dignite.Abp.FlexFields.Web.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.Web;

/// <summary>Exercises the TagHelpers themselves (attribute dispatch, ShowInList, the PartialName
/// escape hatch) on top of the renderer-level coverage in <see cref="FlexFieldViewRendering_Tests"/>
/// and <see cref="FlexFieldSearchRendering_Tests"/>.</summary>
public class FlexFieldTagHelpers_Tests : DigniteAbpFlexFieldsWebTestBase
{
    [Fact]
    public async Task FlexFieldViewTagHelper_dispatches_by_field_type_and_unwraps_its_own_tag()
    {
        var field = new FlexFieldData(Guid.NewGuid(), "Bio", displayName: "Bio", fieldTypeName: TextFieldType.ControlName);
        var value = new FlexFieldValue(field, value: "hello");
        var tagHelper = new FlexFieldViewTagHelper(Renderer) { Field = value };

        var output = await Process(tagHelper, "flex-field-view");

        output.TagName.ShouldBeNull();
        output.Content.GetContent().ShouldContain("hello");
    }

    [Fact]
    public async Task FlexFieldViewTagHelper_PartialName_overrides_the_conventional_lookup()
    {
        var field = new FlexFieldData(Guid.NewGuid(), "Bio", displayName: "Bio", fieldTypeName: "SomeUnregisteredType");
        var value = new FlexFieldValue(field, value: "hello");
        var tagHelper = new FlexFieldViewTagHelper(Renderer)
        {
            Field = value,
            // "SomeUnregisteredType" has no shipped partial - without the override this would throw.
            PartialName = "FlexFields/" + TextFieldType.ControlName
        };

        var output = await Process(tagHelper, "flex-field-view");

        output.Content.GetContent().ShouldContain("hello");
    }

    [Fact]
    public async Task FlexFieldSearchTagHelper_defaults_the_input_name_to_the_fields_own_name()
    {
        var field = new FlexFieldData(Guid.NewGuid(), "keyword", displayName: "Keyword", fieldTypeName: TextFieldType.ControlName);
        var value = new FlexFieldValue(field);
        var tagHelper = new FlexFieldSearchTagHelper(Renderer) { Field = value };

        var output = await Process(tagHelper, "flex-field-search");

        output.Content.GetContent().ShouldContain("name=\"keyword\"");
    }

    private static async Task<TagHelperOutput> Process(TagHelper tagHelper, string tagName)
    {
        var context = new TagHelperContext(
            tagName,
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            Guid.NewGuid().ToString("N"));

        var output = new TagHelperOutput(
            tagName,
            new TagHelperAttributeList(),
            (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        await tagHelper.ProcessAsync(context, output);
        return output;
    }
}
