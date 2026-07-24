using Dignite.NotificationCenter.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace Dignite.NotificationCenter;

/// <summary>
/// Base class for this module's HTTP API controllers. It binds the module's own localization
/// resource so validation / error messages resolve against <see cref="NotificationCenterResource"/>
/// rather than the framework default. This is ABP's per-module controller-base convention (mirroring
/// <c>AbpIdentityController</c> / <c>AbpAccountController</c>); per-controller concerns like
/// <c>[RemoteService]</c>, <c>[Area]</c> and <c>[Route]</c> stay on the concrete controllers.
/// </summary>
public abstract class NotificationCenterController : AbpControllerBase
{
    protected NotificationCenterController()
    {
        LocalizationResource = typeof(NotificationCenterResource);
    }
}
