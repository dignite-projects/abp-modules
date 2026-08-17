using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Boolean;
using Dignite.Abp.FlexFields.Date;
using Dignite.Abp.FlexFields.Number;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Text;
using Dignite.Abp.FlexFields.Tree;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.Web;

/// <summary>Renders each built-in field type's default search partial - proves the inputs a host would
/// actually read back are named the way <see cref="FlexFieldSearchViewModel.Name"/> promises.</summary>
public class FlexFieldSearchRendering_Tests : DigniteAbpFlexFieldsWebTestBase
{
    [Fact]
    public async Task Text_search_renders_a_single_text_input()
    {
        var html = await RenderSearch(TextFieldType.ControlName, new FieldConfigurationDictionary(), "keyword");

        html.ShouldContain("name=\"keyword\"");
        html.ShouldContain("type=\"search\"");
    }

    [Fact]
    public async Task Number_search_renders_independent_min_and_max_inputs()
    {
        var html = await RenderSearch(NumberFieldType.ControlName, new FieldConfigurationDictionary(), "price");

        html.ShouldContain("name=\"priceMin\"");
        html.ShouldContain("name=\"priceMax\"");
    }

    [Fact]
    public async Task DateTime_search_renders_a_date_range_using_the_configured_input_type()
    {
        var configuration = new FieldConfigurationDictionary();
        _ = new DateTimeConfiguration(configuration) { InputMode = DateTimeInputMode.DateTime };

        var html = await RenderSearch(DateTimeFieldType.ControlName, configuration, "createdAt");

        html.ShouldContain("name=\"createdAtFrom\"");
        html.ShouldContain("name=\"createdAtTo\"");
        html.ShouldContain("type=\"datetime-local\"");
    }

    [Fact]
    public async Task Select_search_renders_every_configured_option()
    {
        var configuration = new FieldConfigurationDictionary();
        _ = new SelectConfiguration(configuration)
        {
            Multiple = true,
            Options = new List<SelectListItem> { new("Red", "red", false), new("Blue", "blue", false) }
        };

        var html = await RenderSearch(SelectFieldType.ControlName, configuration, "color");

        html.ShouldContain("name=\"color\"");
        html.ShouldContain("multiple");
        html.ShouldContain("<option value=\"red\">Red</option>");
        html.ShouldContain("<option value=\"blue\">Blue</option>");
    }

    [Fact]
    public async Task Boolean_search_renders_a_tristate_not_a_checkbox()
    {
        var html = await RenderSearch(BooleanFieldType.ControlName, new FieldConfigurationDictionary(), "active");

        html.ShouldContain("<option value=\"\">Any</option>");
        html.ShouldContain("<option value=\"true\">Yes</option>");
        html.ShouldContain("<option value=\"false\">No</option>");
    }

    [Fact]
    public async Task Tree_search_flattens_nested_nodes_into_one_select()
    {
        var configuration = new FieldConfigurationDictionary();
        _ = new Dignite.Abp.FlexFields.Tree.TreeConfiguration(configuration)
        {
            Nodes = new List<Dignite.Abp.FlexFields.Tree.TreeNodeItem>
            {
                new("Root", "root", false)
                {
                    Children = new List<Dignite.Abp.FlexFields.Tree.TreeNodeItem> { new("Child", "child", false) }
                }
            }
        };

        var html = await RenderSearch(TreeFieldType.ControlName, configuration, "category");

        html.ShouldContain("<option value=\"root\">");
        html.ShouldContain("<option value=\"child\">");
        html.ShouldContain("Root");
        html.ShouldContain("Child");
    }

    private async Task<string> RenderSearch(string fieldTypeName, FieldConfigurationDictionary configuration, string name)
    {
        var field = new FlexFieldData(Guid.NewGuid(), name, displayName: name, fieldTypeName: fieldTypeName, configuration: configuration);
        var value = new FlexFieldValue(field);

        return await Renderer.RenderAsync("FlexFields/Search/" + fieldTypeName, new FlexFieldSearchViewModel(value, name));
    }
}
