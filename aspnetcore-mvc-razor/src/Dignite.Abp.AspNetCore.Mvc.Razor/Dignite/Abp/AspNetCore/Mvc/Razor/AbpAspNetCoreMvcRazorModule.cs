using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.AspNetCore.Mvc.UI;
using Volo.Abp.AspNetCore.Mvc.UI.Theming;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.AspNetCore.Mvc.Razor;

/// <summary>
/// Generic ASP.NET Core MVC/Razor infrastructure: <see cref="IRazorPartialRenderer"/> (conventionally
/// registered, no wiring needed here) and <see cref="TenantViewLocationExpander"/> (registered below).
/// Domain-agnostic - references no other module in this repository, and is referenced by any of them
/// that wants either capability.
/// </summary>
[DependsOn(
    typeof(AbpAspNetCoreMvcUiModule)
    )]
public class AbpAspNetCoreMvcRazorModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<RazorViewEngineOptions>(options =>
        {
            var currentTenantLazy = context.Services.GetServiceLazy<ICurrentTenant>();
            var themeSelectorLazy = context.Services.GetServiceLazy<IThemeSelector>();
            options.ViewLocationExpanders.Add(new TenantViewLocationExpander(currentTenantLazy, themeSelectorLazy));
        });
    }
}
