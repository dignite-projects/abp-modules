using System;
using Volo.Abp;

namespace Dignite.Abp.Notifications.Emailing;

public class NotificationEmailBuildContext
{
    public NotificationPayload Notification { get; }

    public Guid UserId { get; }

    public string EmailAddress { get; }

    /// <summary>
    /// The culture selected for this recipient's email content. The empty string is the invariant culture, which is
    /// what the ambient culture degrades to under invariant globalization — so this is not rejected as blank.
    /// </summary>
    public string CultureName { get; }

    public Guid? TenantId { get; }

    public NotificationEmailBuildContext(
        NotificationPayload notification,
        Guid userId,
        string emailAddress,
        Guid? tenantId,
        string cultureName = NotificationEmailOptions.DefaultCultureName)
    {
        Notification = Check.NotNull(notification, nameof(notification));
        UserId = userId;
        EmailAddress = Check.NotNullOrWhiteSpace(emailAddress, nameof(emailAddress));
        TenantId = tenantId;
        CultureName = Check.NotNull(cultureName, nameof(cultureName));
    }
}
