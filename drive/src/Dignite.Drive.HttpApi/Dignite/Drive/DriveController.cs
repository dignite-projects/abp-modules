using Dignite.Drive.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Drive;

public abstract class DriveController : AbpControllerBase
{
    protected DriveController()
    {
        LocalizationResource = typeof(DriveResource);
    }
}
