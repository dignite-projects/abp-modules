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

/// <summary>
/// Renders each built-in field type's default view partial through the real
/// <c>IRazorPartialRenderer</c> pipeline - proves the "FlexFields/{FieldTypeName}" convention actually
/// resolves the shipped, precompiled .cshtml (not just that it compiles), and that each type's Razor
/// logic reads its configuration/value correctly.
/// </summary>
public class FlexFieldViewRendering_Tests : DigniteAbpFlexFieldsWebTestBase
{
    [Fact]
    public async Task Renders_TextEdit_preserving_line_breaks()
    {
        var configuration = new FieldConfigurationDictionary();
        _ = new TextConfiguration(configuration) { Mode = TextMode.MultipleLine };
        var field = CreateField("Bio", TextFieldType.ControlName, configuration);
        var value = new FlexFieldValue(field, value: "Hello\nWorld");

        var html = await Renderer.RenderAsync("FlexFields/" + TextFieldType.ControlName, new FlexFieldViewModel(value, showInList: false));

        html.ShouldContain("Hello");
        html.ShouldContain("World");
        html.ShouldContain("white-space: pre-line");
        html.ShouldContain(field.DisplayName);
    }

    [Fact]
    public async Task Renders_NumericEdit_with_configured_decimals()
    {
        var field = CreateField("Price", NumberFieldType.ControlName, new FieldConfigurationDictionary());
        var value = new FlexFieldValue(field, value: 1234.5m);

        var html = await Renderer.RenderAsync("FlexFields/" + NumberFieldType.ControlName, new FlexFieldViewModel(value, showInList: false));

        html.ShouldContain(1234.5m.ToString("N2"));
    }

    [Fact]
    public async Task Renders_DateEdit_as_date_only_by_default()
    {
        var field = CreateField("Birthday", DateTimeFieldType.ControlName, new FieldConfigurationDictionary());
        var value = new FlexFieldValue(field, value: new DateTime(2026, 1, 15));

        var html = await Renderer.RenderAsync("FlexFields/" + DateTimeFieldType.ControlName, new FlexFieldViewModel(value, showInList: false));

        html.ShouldContain(new DateTime(2026, 1, 15).ToString("d"));
    }

    [Fact]
    public async Task Renders_Select_as_the_matching_option_label_not_the_raw_value()
    {
        var configuration = new FieldConfigurationDictionary();
        _ = new SelectConfiguration(configuration)
        {
            Options = new List<SelectListItem>
            {
                new("Red", "red", false),
                new("Blue", "blue", false)
            }
        };
        var field = CreateField("Color", SelectFieldType.ControlName, configuration);
        var value = new FlexFieldValue(field, value: "red");

        var html = await Renderer.RenderAsync("FlexFields/" + SelectFieldType.ControlName, new FlexFieldViewModel(value, showInList: false));

        html.ShouldContain("Red");
        // Shouldly's string ShouldNotContain is case-insensitive by default, which can't tell "Red"
        // (the label, correct) from "red" (the raw stored value, would be a bug) apart - Case.Sensitive
        // is required to actually test this.
        html.ShouldNotContain(">red<", Case.Sensitive);
    }

    [Fact]
    public async Task Renders_Switch_as_Yes_or_No_not_the_raw_boolean()
    {
        var field = CreateField("Active", BooleanFieldType.ControlName, new FieldConfigurationDictionary());
        var value = new FlexFieldValue(field, value: true);

        var html = await Renderer.RenderAsync("FlexFields/" + BooleanFieldType.ControlName, new FlexFieldViewModel(value, showInList: false));

        html.ShouldContain("Yes");
        // Not a blanket ShouldNotContain("True"): the view deliberately also emits a lowercase
        // data-value="true" attribute as a CSS/JS hook, which a case-insensitive contains would also
        // (wrongly) flag. ">True<" targets only the visible text node.
        html.ShouldNotContain(">True<");
    }

    [Fact]
    public async Task Renders_TreeView_resolving_a_nested_child_nodes_label()
    {
        var configuration = new FieldConfigurationDictionary();
        _ = new TreeConfiguration(configuration)
        {
            Nodes = new List<TreeNodeItem>
            {
                new("Root", "root", false)
                {
                    Children = new List<TreeNodeItem> { new("Child", "child", false) }
                }
            }
        };
        var field = CreateField("Category", TreeFieldType.ControlName, configuration);
        var value = new FlexFieldValue(field, value: "child");

        var html = await Renderer.RenderAsync("FlexFields/" + TreeFieldType.ControlName, new FlexFieldViewModel(value, showInList: false));

        html.ShouldContain("Child");
    }

    [Fact]
    public async Task ShowInList_renders_bare_value_without_the_label_wrapper()
    {
        var field = CreateField("Active", BooleanFieldType.ControlName, new FieldConfigurationDictionary());
        var value = new FlexFieldValue(field, value: true);

        var html = await Renderer.RenderAsync("FlexFields/" + BooleanFieldType.ControlName, new FlexFieldViewModel(value, showInList: true));

        html.ShouldContain("Yes");
        html.ShouldNotContain("flex-field-view");
        html.ShouldNotContain(field.DisplayName);
    }

    private static FlexFieldData CreateField(string name, string fieldTypeName, FieldConfigurationDictionary configuration)
    {
        return new FlexFieldData(Guid.NewGuid(), name, displayName: name, fieldTypeName: fieldTypeName, configuration: configuration);
    }
}
