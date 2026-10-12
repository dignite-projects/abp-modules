using Dignite.Abp.Notifications;
using Dignite.NotificationCenter.Localization;
using Volo.Abp.Application.Services;
using Volo.Abp.Localization;

namespace Dignite.NotificationCenter;

/// <summary>
/// Base class for this module's application services: binds <see cref="NotificationCenterResource"/> (ABP's
/// per-module app-service-base convention) and resolves group display names the same way for every service.
/// </summary>
public abstract class NotificationCenterAppService : ApplicationService
{
    protected NotificationCenterAppService()
    {
        LocalizationResource = typeof(NotificationCenterResource);
    }

    /// <summary>
    /// Localized display name of a definition group, or of the synthetic
    /// <see cref="NotificationCenterConsts.OtherGroupName"/> bucket.
    /// </summary>
    protected virtual string GetGroupDisplayName(NotificationGroupDefinition? group)
    {
        return group == null
            ? L["NotificationGroup:Other"]
            : group.DisplayName.Localize(StringLocalizerFactory).Value;
    }
}
