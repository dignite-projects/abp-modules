using Dignite.Abp.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Modularity;

namespace Dignite.Abp.FlexFields.Web;

/// <summary>
/// Server-side (Razor) display and search rendering for flex fields: the
/// <c>&lt;flex-field-view&gt;</c>/<c>&lt;flex-field-search&gt;</c> TagHelpers (under
/// <c>TagHelpers/</c>) and their default views for the six built-in field types (under
/// <c>Views/Shared/FlexFields/</c>) - the non-Angular counterpart to the Angular library's
/// <c>&lt;ff-flex-field-view&gt;</c>/<c>&lt;ff-flex-field-search&gt;</c>. No config/control
/// TagHelpers - this package is display and search only, the same split the Angular library keeps
/// between its four component kinds.
/// <para>
/// The one thing a consuming host still has to do itself is add
/// <c>@addTagHelper *, Dignite.Abp.FlexFields.Web</c> to its own <c>_ViewImports.cshtml</c> - TagHelper
/// discovery is a per-Razor-compilation directive, not something a module class can register on a
/// host's behalf.
/// </para>
/// </summary>
[DependsOn(
    typeof(FlexFieldsAbstractionsModule),
    typeof(AbpAspNetCoreMvcRazorModule)
    )]
public class FlexFieldsWebModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<IMvcBuilder>(mvcBuilder =>
        {
            mvcBuilder.AddCompiledRazorAssemblyPartIfNotExists(typeof(FlexFieldsWebModule).Assembly);
        });
    }
}
