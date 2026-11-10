using Volo.Abp.Application.Services;
using Dignite.Drive.Host.Localization;

namespace Dignite.Drive.Host.Services;

/* Inherit your application services from this class. */
public abstract class HostAppService : ApplicationService
{
    protected HostAppService()
    {
        LocalizationResource = typeof(HostResource);
    }
}