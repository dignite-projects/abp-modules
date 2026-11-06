using System;
using System.Threading.Tasks;
using Dignite.Abp.FlexFields.CKEditor;
using Shouldly;
using Xunit;

namespace Dignite.Abp.FlexFields.Web;

/// <summary>
/// The CKEditor field's view (<c>Dignite.Abp.FlexFields.CKEditor.Web</c>) renders a value through the module's
/// sanitizer: what the toolbar can make survives, script does not.
/// </summary>
public class CKEditorViewRendering_Tests : DigniteAbpFlexFieldsWebTestBase
{
    [Fact]
    public async Task Links_To_An_Email_Address_Or_A_Phone_Number_Should_Keep_Their_Address()
    {
        var html = await RenderAsync(
            "<p>Write to <a href=\"mailto:hello@example.com\">hello@example.com</a> or call <a href=\"tel:+81-6-0000-0000\">us</a>.</p>");

        html.ShouldContain("href=\"mailto:hello@example.com\"");
        html.ShouldContain("href=\"tel:+81-6-0000-0000\"");
    }

    [Fact]
    public async Task Script_Should_Still_Be_Removed()
    {
        var html = await RenderAsync(
            "<p><a href=\"javascript:alert(1)\">x</a><img src=\"x\" onerror=\"alert(2)\"><script>alert(3)</script></p>");

        html.ShouldNotContain("javascript:");
        html.ShouldNotContain("onerror");
        html.ShouldNotContain("<script");
    }

    private async Task<string> RenderAsync(string value)
    {
        var field = new FlexFieldData(Guid.NewGuid(), "body", displayName: "Body", fieldTypeName: CKEditorFieldType.ControlName,
            configuration: new FieldConfigurationDictionary());

        return await Renderer.RenderAsync("FlexFields/" + CKEditorFieldType.ControlName,
            new FlexFieldViewModel(new FlexFieldValue(field, value: value), showInList: true));
    }
}
