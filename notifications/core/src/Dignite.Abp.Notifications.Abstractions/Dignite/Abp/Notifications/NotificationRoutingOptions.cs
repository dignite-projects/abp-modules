using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Maps notification names to the external channels they are delivered on. Modules add defaults for their own
/// notifications; the host module, configured last, overrides any of them or sets the fallback. The contracts know no
/// channel names — they are the open set registered through <see cref="NotificationNotifierOptions"/>.
/// </summary>
public class NotificationRoutingOptions
{
    /// <summary>Channels for notifications without an explicit rule. Null or empty = inbox-only.</summary>
    public string[]? Default { get; set; }

    /// <summary>
    /// Notification name → channels. An empty array is an explicit inbox-only rule and is different from no rule.
    /// Keys use ordinal, case-sensitive comparison (notification names); channel names are compared ordinal,
    /// case-insensitive, like <see cref="NotificationNotifierOptions"/>.
    /// </summary>
    public IDictionary<string, string[]> Notifications { get; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal);

    /// <summary>
    /// Opt-in: fail startup instead of warning when a rule names a channel this process does not host.
    /// </summary>
    public bool RequireHostedChannels { get; set; }

    /// <summary>
    /// Routes a notification to the given channels. At least one channel is required; use
    /// <see cref="InboxOnly"/> for the explicit no-external-channel rule. A later call for the same name replaces
    /// the earlier rule as a whole.
    /// </summary>
    public NotificationRoutingOptions ForNotification(string notificationName, params string[] channels)
    {
        Check.NotNullOrWhiteSpace(notificationName, nameof(notificationName));

        if (channels == null || channels.Length == 0)
        {
            throw new ArgumentException(
                "At least one notification channel must be specified. Use InboxOnly(...) for an explicit inbox-only rule.",
                nameof(channels));
        }

        Notifications[notificationName] = NormalizeChannels(channels);
        return this;
    }

    /// <summary>Applies <see cref="ForNotification"/> to every name with the same channels.</summary>
    public NotificationRoutingOptions ForNotifications(IEnumerable<string> notificationNames, params string[] channels)
    {
        Check.NotNull(notificationNames, nameof(notificationNames));

        foreach (var notificationName in notificationNames)
        {
            ForNotification(notificationName, channels);
        }

        return this;
    }

    /// <summary>Explicit inbox-only rule: overrides <see cref="Default"/> for these notifications.</summary>
    public NotificationRoutingOptions InboxOnly(params string[] notificationNames)
    {
        Check.NotNull(notificationNames, nameof(notificationNames));

        foreach (var notificationName in notificationNames)
        {
            Check.NotNullOrWhiteSpace(notificationName, nameof(notificationNames));
            Notifications[notificationName] = Array.Empty<string>();
        }

        return this;
    }

    /// <summary>
    /// Trims, rejects blank names and de-duplicates (ordinal, ignore-case) a channel list. Shared with the default
    /// resolver so values assigned directly to <see cref="Default"/> or <see cref="Notifications"/> are treated the
    /// same as those written through the fluent methods.
    /// </summary>
    internal static string[] NormalizeChannels(IEnumerable<string> channels)
    {
        var list = channels.ToList();
        if (list.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Notification channel names cannot be null, empty or whitespace.", nameof(channels));
        }

        return list.Select(channel => channel.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal void Validate()
    {
        if (Default != null && Default.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException(
                $"{nameof(NotificationRoutingOptions)}.{nameof(Default)} contains a null, empty or whitespace channel name.");
        }

        foreach (var rule in Notifications)
        {
            var notificationName = rule.Key;
            var channels = rule.Value;

            if (string.IsNullOrWhiteSpace(notificationName))
            {
                throw new InvalidOperationException(
                    $"{nameof(NotificationRoutingOptions)}.{nameof(Notifications)} contains a rule with a blank notification name.");
            }

            if (channels == null || channels.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException(
                    $"{nameof(NotificationRoutingOptions)}.{nameof(Notifications)}['{notificationName}'] contains a null, empty or whitespace channel name.");
            }
        }
    }
}
