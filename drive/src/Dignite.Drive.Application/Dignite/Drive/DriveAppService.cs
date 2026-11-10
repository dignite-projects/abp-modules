using Dignite.Drive.Localization;
using Volo.Abp.Application.Services;

namespace Dignite.Drive;

public abstract class DriveAppService : ApplicationService
{
    protected DriveAppService()
    {
        LocalizationResource = typeof(DriveResource);
        ObjectMapperContext = typeof(DriveApplicationModule);
    }
}
