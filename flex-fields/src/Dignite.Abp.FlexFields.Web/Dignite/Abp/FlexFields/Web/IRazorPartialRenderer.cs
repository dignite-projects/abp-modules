using System.Threading.Tasks;

namespace Dignite.Abp.FlexFields.Web;

/// <summary>
/// Renders a partial view to an HTML string from outside a controller action - e.g. from a
/// <c>TagHelper</c> that dispatches to one of several partials by some runtime key. Needs a real
/// <see cref="Microsoft.AspNetCore.Http.HttpContext"/> to be current, so it only works while a real
/// HTTP request is being handled - which a <c>TagHelper</c> always is.
/// </summary>
public interface IRazorPartialRenderer
{
    /// <summary>
    /// Renders <paramref name="partialName"/> with <paramref name="model"/>, resolving the view the
    /// same way <c>&lt;partial name="..." /&gt;</c> would: relative to the current controller/page
    /// first, then <c>Views/Shared</c> - so a caller can pass either a bare conventional name (e.g.
    /// <c>"FlexFields/Text"</c>) or a rooted path (e.g. <c>"~/Views/Shared/Custom.cshtml"</c>).
    /// </summary>
    Task<string> RenderAsync<TModel>(string partialName, TModel model);
}
