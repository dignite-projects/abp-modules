using System;
using System.Linq;

namespace Dignite.Abp.Notifications;

/// <summary>Helpers for routing a notification to specific notifier channels.</summary>
public static class NotificationChannels
{
    /// <summary><see cref="NotificationData"/>-independent attribute key holding the allowed channel names (string[]).</summary>
    public const string AttributeName = "Dignite.Notifications.Channels";

    /// <summary>
    /// Whether a notifier named <paramref name="notifierName"/> may deliver a notification whose allowed channels
    /// are <paramref name="allowedChannels"/>. Null or empty means "no external channel".
    /// </summary>
    public static bool IsAllowed(string[]? allowedChannels, string notifierName)
    {
        return allowedChannels != null
            && allowedChannels.Length > 0
            && allowedChannels.Contains(notifierName, StringComparer.OrdinalIgnoreCase);
    }
}
