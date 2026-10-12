using System;
using Volo.Abp;

namespace Dignite.Abp.Notifications.Push;

/// <summary>
/// What a push content provider builds from. Carries no device: content is built once per culture and shared by every
/// device of the recipient registered in that culture.
/// </summary>
public class NotificationPushBuildContext
{
    public NotificationPayload Notification { get; }

    public Guid UserId { get; }

    /// <summary>
    /// The culture this content is built in, also the ambient culture during the build. The empty string is the
    /// invariant culture, which is what the ambient culture degrades to under invariant globalization — so this is not
    /// rejected as blank.
    /// </summary>
    public string CultureName { get; }

    public Guid? TenantId { get; }

    public NotificationPushBuildContext(
        NotificationPayload notification,
        Guid userId,
        Guid? tenantId,
        string cultureName = NotificationPushOptions.DefaultCultureName)
    {
        Notification = Check.NotNull(notification, nameof(notification));
        UserId = userId;
        TenantId = tenantId;
        CultureName = Check.NotNull(cultureName, nameof(cultureName));
    }
}
