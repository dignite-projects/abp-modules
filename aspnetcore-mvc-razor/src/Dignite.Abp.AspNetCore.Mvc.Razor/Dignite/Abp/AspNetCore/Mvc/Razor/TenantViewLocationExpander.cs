using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Razor;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc.UI.Theming;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.AspNetCore.Mvc.Razor;

/// <summary>
/// Tries tenant- and theme-specific view locations before the host's normal ones, so a tenant (or a
/// theme) can override any <c>.cshtml</c> the current lookup would otherwise resolve, simply by
/// placing a file at the same relative path under <c>/Tenants/{tenantName}/...</c> and/or
/// <c>/Themes/{themeName}/...</c>. Falls through to the unmodified candidate locations when nothing
/// more specific exists, so this is purely additive - safe to have registered even for a host that
/// never ends up using either override.
/// <para>
/// Registered automatically by <see cref="AbpAspNetCoreMvcRazorModule"/> - a consumer only needs the
/// module dependency, not a hand-written <c>Configure&lt;RazorViewEngineOptions&gt;</c>.
/// </para>
/// </summary>
public class TenantViewLocationExpander : IViewLocationExpander
{
    private const string TenancyNameKey = "dignite:tenancyName";
    private const string ThemeNameKey = "dignite:themeName";

    private readonly Lazy<ICurrentTenant?> _currentTenantLazy;
    private readonly Lazy<IThemeSelector?> _themeSelectorLazy;

    public TenantViewLocationExpander(Lazy<ICurrentTenant?> currentTenantLazy, Lazy<IThemeSelector?> themeSelectorLazy)
    {
        _currentTenantLazy = currentTenantLazy;
        _themeSelectorLazy = themeSelectorLazy;
    }

    public void PopulateValues(ViewLocationExpanderContext context)
    {
        var currentTenant = _currentTenantLazy.Value;
        if (!context.Values.ContainsKey(TenancyNameKey) && currentTenant is { IsAvailable: true })
        {
            var tenantName = currentTenant.Name;
            if (!string.IsNullOrEmpty(tenantName))
            {
                context.Values[TenancyNameKey] = tenantName;
            }
        }

        if (!context.Values.ContainsKey(ThemeNameKey))
        {
            var themeName = TryGetCurrentThemeName();
            if (!string.IsNullOrEmpty(themeName))
            {
                context.Values[ThemeNameKey] = themeName;
            }
        }
    }

    /// <summary>
    /// Theming is optional for this expander - most hosts in practice have no theme package installed
    /// at all, and ABP's own <c>DefaultThemeSelector</c> throws <see cref="AbpException"/> rather than
    /// returning a "no theme" default when nothing is registered via <c>AbpThemingOptions</c>. Treat
    /// that as "no theme," not a reason to fail view resolution - tenant-only prefixing still applies.
    /// </summary>
    private string? TryGetCurrentThemeName()
    {
        var themeSelector = _themeSelectorLazy.Value;
        if (themeSelector == null)
        {
            return null;
        }

        try
        {
            return themeSelector.GetCurrentThemeInfo().Name;
        }
        catch (AbpException)
        {
            return null;
        }
    }

    public IEnumerable<string> ExpandViewLocations(ViewLocationExpanderContext context, IEnumerable<string> viewLocations)
    {
        context.Values.TryGetValue(TenancyNameKey, out var tenantName);
        context.Values.TryGetValue(ThemeNameKey, out var themeName);

        if (string.IsNullOrEmpty(tenantName) && string.IsNullOrEmpty(themeName))
        {
            return viewLocations;
        }

        // Operates as a transform over whatever locations the framework (and any earlier-registered
        // expander) already produced - for Areas, Razor Pages, or plain MVC alike - rather than
        // hardcoding a parallel copy of the default view location formats that could drift out of sync
        // with them.
        var baseLocations = viewLocations.ToList();
        var prefixed = new List<string>();

        foreach (var location in baseLocations)
        {
            if (!string.IsNullOrEmpty(tenantName))
            {
                if (!string.IsNullOrEmpty(themeName))
                {
                    prefixed.Add($"/Tenants/{tenantName}/Themes/{themeName}{location}");
                }

                prefixed.Add($"/Tenants/{tenantName}{location}");
            }

            if (!string.IsNullOrEmpty(themeName))
            {
                prefixed.Add($"/Themes/{themeName}{location}");
            }
        }

        prefixed.AddRange(baseLocations);
        return prefixed;
    }
}
