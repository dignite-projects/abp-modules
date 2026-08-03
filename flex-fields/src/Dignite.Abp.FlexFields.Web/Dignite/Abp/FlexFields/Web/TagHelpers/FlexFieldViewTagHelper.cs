using System.Threading.Tasks;
using Dignite.Abp.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Dignite.Abp.FlexFields.Web.TagHelpers;

/// <summary>
/// Renders one flex field's value read-only, dispatched by <see cref="FlexFieldValue.FieldTypeName"/> -
/// the server-side counterpart to the Angular library's <c>&lt;ff-flex-field-view&gt;</c>.
/// <para>
/// Zero IO, by design: the caller supplies an already-resolved <see cref="FlexFieldValue"/> - the
/// kernel has no application service of its own to look one up with (see the
/// FlexFields.Abstractions/Domain split), so assembling it is the host's job, the same boundary the
/// kernel draws everywhere else.
/// </para>
/// </summary>
[HtmlTargetElement("flex-field-view")]
public class FlexFieldViewTagHelper : TagHelper
{
    private const string ViewPathPrefix = "FlexFields/";

    private readonly IRazorPartialRenderer _renderer;

    public FlexFieldViewTagHelper(IRazorPartialRenderer renderer)
    {
        _renderer = renderer;
    }

    /// <summary>The field to render.</summary>
    public FlexFieldValue Field { get; set; } = default!;

    /// <summary>Renders bare, without a label wrapper, for use inside a table cell.</summary>
    public bool ShowInList { get; set; }

    /// <summary>
    /// Overrides the conventional <c>"FlexFields/{Field.FieldTypeName}"</c> partial view lookup - the
    /// same escape hatch <c>&lt;flex-field-search&gt;</c> and CMS's own field TagHelper offer.
    /// </summary>
    public string? PartialName { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = await _renderer.RenderAsync(
            PartialName ?? ViewPathPrefix + Field.FieldTypeName,
            new FlexFieldViewModel(Field, ShowInList));

        output.TagName = null;
        output.Content.SetHtmlContent(body);
    }
}
