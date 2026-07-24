using Microsoft.Extensions.Localization;
using Dignite.NotificationCenter.Web.Host.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace Dignite.NotificationCenter.Web.Host;

[Dependency(ReplaceServices = true)]
public class HostBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<HostResource> _localizer;

    public HostBrandingProvider(IStringLocalizer<HostResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}