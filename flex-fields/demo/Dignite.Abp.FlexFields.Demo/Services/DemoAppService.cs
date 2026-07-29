using Volo.Abp.Application.Services;
using Dignite.Abp.FlexFields.Demo.Localization;

namespace Dignite.Abp.FlexFields.Demo.Services;

/* Inherit your application services from this class. */
public abstract class DemoAppService : ApplicationService
{
    protected DemoAppService()
    {
        LocalizationResource = typeof(DemoResource);
    }
}