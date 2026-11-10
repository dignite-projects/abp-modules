using Microsoft.Extensions.Localization;
using Dignite.Drive.Host.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace Dignite.Drive.Host;

[Dependency(ReplaceServices = true)]
public class HostBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<HostResource> _localizer;

    public HostBrandingProvider(IStringLocalizer<HostResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];

    public override string? LogoUrl => "~/images/logo/leptonxlite/logo-light-thumbnail.png";

    public override string? LogoIconUrl => "~/images/logo/leptonxlite/logo-dark-thumbnail.png";
}