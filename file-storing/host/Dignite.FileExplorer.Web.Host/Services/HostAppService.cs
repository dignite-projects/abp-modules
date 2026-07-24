using Volo.Abp.Application.Services;
using Dignite.FileExplorer.Web.Host.Localization;

namespace Dignite.FileExplorer.Web.Host.Services;

/* Inherit your application services from this class. */
public abstract class HostAppService : ApplicationService
{
    protected HostAppService()
    {
        LocalizationResource = typeof(HostResource);
    }
}