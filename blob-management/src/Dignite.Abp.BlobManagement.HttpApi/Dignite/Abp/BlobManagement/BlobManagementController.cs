using Dignite.Abp.BlobManagement.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.Abp.BlobManagement;

public abstract class BlobManagementController : AbpControllerBase
{
    protected BlobManagementController()
    {
        LocalizationResource = typeof(BlobManagementResource);
    }
}
