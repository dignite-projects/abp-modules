using System.Threading.Tasks;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Dignite.Abp.FlexFields.Web.TagHelpers;

/// <summary>
/// Renders a search/filter input for one flex field, dispatched by
/// <see cref="FlexFieldValue.FieldTypeName"/> - the server-side counterpart to the Angular library's
/// <c>&lt;ff-flex-field-search&gt;</c>.
/// <para>
/// Renders inputs only. Translating whatever gets submitted into a <c>FlexFieldQueryCondition</c> is
/// the host's job - the same boundary the Angular library draws (it can't do that translation either;
/// see the FlexFields demo's <c>ProductsComponent</c>, which owns it for exactly that reason).
/// </para>
/// </summary>
[HtmlTargetElement("flex-field-search")]
public class FlexFieldSearchTagHelper : TagHelper
{
    private const string ViewPathPrefix = "FlexFields/Search/";

    private readonly IRazorPartialRenderer _renderer;

    public FlexFieldSearchTagHelper(IRazorPartialRenderer renderer)
    {
        _renderer = renderer;
    }

    /// <summary>The field to render a search input for.</summary>
    public FlexFieldValue Field { get; set; } = default!;

    /// <summary>
    /// Base HTML form control name for the rendered input(s). Defaults to
    /// <see cref="FlexFieldValue.Name"/> when not set.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Overrides the conventional <c>"FlexFields/Search/{Field.FieldTypeName}"</c> partial
    /// view lookup.</summary>
    public string? PartialName { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = await _renderer.RenderAsync(
            PartialName ?? ViewPathPrefix + Field.FieldTypeName,
            new FlexFieldSearchViewModel(Field, Name ?? Field.Name));

        output.TagName = null;
        output.Content.SetHtmlContent(body);
    }
}
