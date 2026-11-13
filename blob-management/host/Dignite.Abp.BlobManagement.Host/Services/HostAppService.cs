using Volo.Abp.Application.Services;
using Dignite.Abp.BlobManagement.Host.Localization;

namespace Dignite.Abp.BlobManagement.Host.Services;

/* Inherit your application services from this class. */
public abstract class HostAppService : ApplicationService
{
    protected HostAppService()
    {
        LocalizationResource = typeof(HostResource);
    }
}