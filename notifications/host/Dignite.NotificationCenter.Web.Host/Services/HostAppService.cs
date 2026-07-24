using Volo.Abp.Application.Services;
using Dignite.NotificationCenter.Web.Host.Localization;

namespace Dignite.NotificationCenter.Web.Host.Services;

/* Inherit your application services from this class. */
public abstract class HostAppService : ApplicationService
{
    protected HostAppService()
    {
        LocalizationResource = typeof(HostResource);
    }
}