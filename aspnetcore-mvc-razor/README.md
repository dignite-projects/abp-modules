# Dignite.Abp.AspNetCore.Mvc.Razor

> Part of [**dignite-projects/abp-modules**](https://github.com/dignite-projects/abp-modules) — see
> the [repository README](../README.md) for the other modules, and
> [CONTRIBUTING.md](../CONTRIBUTING.md) for the build, versioning, and release process shared across
> them.

Generic, domain-agnostic **ASP.NET Core MVC / Razor infrastructure** for **[ABP Framework](https://abp.io)**
solutions — not a module, and not owned by any of this repository's three module trees. It exists so
that any of them (or a downstream host) can render Razor content outside a controller action, and
resolve views per tenant, without each one rolling its own copy.

- **Render a partial to a string.** `IRazorPartialRenderer` renders a named partial view + model to
  an HTML string from anywhere a real `HttpContext` is available (a `TagHelper`, for example) —
  useful for TagHelpers that dispatch to one of several partials by some runtime key (see
  `Dignite.Abp.FlexFields.Web`'s field-type-driven TagHelpers for a consumer).
- **Tenant-aware view resolution.** `TenantViewLocationExpander` (registered automatically by
  `AbpAspNetCoreMvcRazorModule` — just add the module dependency, no manual
  `Configure<RazorViewEngineOptions>` needed) tries `/Tenants/{tenant}/Themes/{theme}/...`, then
  `/Tenants/{tenant}/...`, then `/Themes/{theme}/...` ahead of the normal search paths, falling back
  to the host's default view when no tenant/theme-specific override exists. Purely additive — safe to
  depend on even when multi-tenancy or theming isn't in use.

> **.NET 10 · ABP 10.5.0 · LGPL-3.0-only**

## Install

```bash
dotnet add package Dignite.Abp.AspNetCore.Mvc.Razor
```

```csharp
[DependsOn(typeof(AbpAspNetCoreMvcRazorModule))]
public class YourModule : AbpModule
{
}
```
