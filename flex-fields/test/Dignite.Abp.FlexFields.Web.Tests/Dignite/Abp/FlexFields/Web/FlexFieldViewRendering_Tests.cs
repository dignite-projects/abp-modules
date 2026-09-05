using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.Boolean;
using Dignite.Abp.FlexFields.Date;
using Dignite.Abp.FlexFields.Matrix;
using Dignite.Abp.FlexFields.Number;
using Dignite.Abp.FlexFields.Select;
using Dignite.Abp.FlexFields.Table;
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
    public async Task Renders_Text_preserving_line_breaks()
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
    public async Task Renders_Number_with_configured_decimals()
    {
        var field = CreateField("Price", NumberFieldType.ControlName, new FieldConfigurationDictionary());
        var value = new FlexFieldValue(field, value: 1234.5m);

        var html = await Renderer.RenderAsync("FlexFields/" + NumberFieldType.ControlName, new FlexFieldViewModel(value, showInList: false));

        html.ShouldContain(1234.5m.ToString("N2"));
    }

    [Fact]
    public async Task Renders_DateTime_as_date_only_by_default()
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
    public async Task Renders_Boolean_as_Yes_or_No_not_the_raw_boolean()
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
    public async Task Renders_Tree_resolving_a_nested_child_nodes_label()
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

    /// <summary>
    /// The composite case: <c>Matrix.cshtml</c> does not render sub-field values itself, it recurses
    /// through the same <c>&lt;flex-field-view&gt;</c> dispatch one level deeper - so this asserts the
    /// Text sub-field's own partial actually ran inside the block, not just that the block wrapper was
    /// emitted.
    /// </summary>
    [Fact]
    public async Task Renders_Matrix_recursing_into_each_blocks_sub_fields()
    {
        var configuration = new FieldConfigurationDictionary();
        _ = new MatrixConfiguration(configuration)
        {
            BlockTypes = new List<MatrixBlockType>
            {
                new()
                {
                    Name = "quote",
                    DisplayName = "Quote Block",
                    Fields = new List<InlineFieldDefinition>
                    {
                        new() { Name = "text", DisplayName = "Quote Text", FieldTypeName = TextFieldType.ControlName }
                    }
                }
            }
        };
        var field = CreateField("Sections", MatrixFieldType.ControlName, configuration);
        var blocks = new List<MatrixBlockValue>
        {
            new()
            {
                BlockTypeName = "quote",
                Values = new FlexFieldDictionary { ["text"] = "Hello from a block" }
            }
        };
        var value = new FlexFieldValue(field, value: blocks);

        var html = await Renderer.RenderAsync("FlexFields/" + MatrixFieldType.ControlName, new FlexFieldViewModel(value, showInList: false));

        html.ShouldContain("data-block-type=\"quote\"");
        html.ShouldContain("Quote Block");
        // Only the recursion can have produced these two: the label and the value both come from the
        // Text partial, which Matrix.cshtml never writes itself.
        html.ShouldContain("Quote Text");
        html.ShouldContain("Hello from a block");
    }

    [Fact]
    public async Task Renders_Table_as_a_column_header_row_and_one_row_per_value()
    {
        var configuration = new FieldConfigurationDictionary();
        _ = new TableConfiguration(configuration)
        {
            Columns = new List<InlineFieldDefinition>
            {
                new() { Name = "name", DisplayName = "Spec Name", FieldTypeName = TextFieldType.ControlName },
                new() { Name = "value", DisplayName = "Spec Value", FieldTypeName = TextFieldType.ControlName }
            }
        };
        var field = CreateField("Specs", TableFieldType.ControlName, configuration);
        var rows = new List<TableRow>
        {
            new() { Values = new FlexFieldDictionary { ["name"] = "Weight", ["value"] = "180 g" } }
        };
        var value = new FlexFieldValue(field, value: rows);

        var html = await Renderer.RenderAsync("FlexFields/" + TableFieldType.ControlName, new FlexFieldViewModel(value, showInList: false));

        html.ShouldContain("<th>Spec Name</th>");
        html.ShouldContain("<th>Spec Value</th>");
        // Cells render show-in-list, so the sub-field's Text partial emits the bare value with no label
        // wrapper of its own inside the <td>.
        html.ShouldContain("Weight");
        html.ShouldContain("180 g");
    }

    [Fact]
    public async Task ShowInList_renders_Matrix_as_a_block_count()
    {
        var field = CreateField("Sections", MatrixFieldType.ControlName, new FieldConfigurationDictionary());
        var blocks = new List<MatrixBlockValue>
        {
            new() { BlockTypeName = "quote" },
            new() { BlockTypeName = "quote" }
        };
        var value = new FlexFieldValue(field, value: blocks);

        var html = await Renderer.RenderAsync("FlexFields/" + MatrixFieldType.ControlName, new FlexFieldViewModel(value, showInList: true));

        html.ShouldContain("2 block(s)");
        html.ShouldNotContain("flex-field-view");
    }

    [Fact]
    public async Task ShowInList_renders_Table_as_a_row_count()
    {
        var field = CreateField("Specs", TableFieldType.ControlName, new FieldConfigurationDictionary());
        var rows = new List<TableRow> { new(), new(), new() };
        var value = new FlexFieldValue(field, value: rows);

        var html = await Renderer.RenderAsync("FlexFields/" + TableFieldType.ControlName, new FlexFieldViewModel(value, showInList: true));

        html.ShouldContain("3 row(s)");
        html.ShouldNotContain("flex-field-view");
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
