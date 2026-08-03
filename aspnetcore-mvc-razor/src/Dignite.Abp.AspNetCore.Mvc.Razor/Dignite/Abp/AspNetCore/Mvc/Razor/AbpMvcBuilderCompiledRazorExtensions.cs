using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;

namespace Dignite.Abp.AspNetCore.Mvc.Razor;

public static class AbpMvcBuilderCompiledRazorExtensions
{
    /// <summary>
    /// Registers <paramref name="assembly"/>'s precompiled Razor views (TagHelpers/controllers are
    /// unaffected either way) so <c>IRazorViewEngine</c> can actually find them.
    /// <para>
    /// Deliberately not ABP's own <c>AddApplicationPartIfNotExists</c> (nor the automatic
    /// module-assembly registration <c>AbpAspNetCoreMvcModule.OnApplicationInitialization</c> does):
    /// both add a bare <see cref="AssemblyPart"/>, which <c>IViewsFeatureProvider</c> never surfaces
    /// views from - only a <see cref="CompiledRazorAssemblyPart"/> (<c>IRazorCompiledItemProvider</c>)
    /// is discovered as a view source. A real ASP.NET Core host's own
    /// <c>AddMvc()</c>/<c>AddControllersWithViews()</c> call normally adds every referenced assembly
    /// the correct way via its own default-parts scan (keyed off the entry assembly), which is why most
    /// MVC UI libraries never need this explicitly - but that scan is not guaranteed in every hosting
    /// scenario (an <c>AbpIntegratedTest</c>-based test host is a confirmed case where it does not run
    /// at all). Going through the real <see cref="ApplicationPartFactory"/> removes the dependency on
    /// that scan succeeding.
    /// </para>
    /// </summary>
    public static void AddCompiledRazorAssemblyPartIfNotExists(this IMvcBuilder mvcBuilder, Assembly assembly)
    {
        var alreadyAdded = mvcBuilder.PartManager.ApplicationParts
            .OfType<CompiledRazorAssemblyPart>()
            .Any(part => part.Assembly == assembly);

        if (alreadyAdded)
        {
            return;
        }

        foreach (var part in ApplicationPartFactory.GetApplicationPartFactory(assembly).GetApplicationParts(assembly))
        {
            mvcBuilder.PartManager.ApplicationParts.Add(part);
        }
    }
}
