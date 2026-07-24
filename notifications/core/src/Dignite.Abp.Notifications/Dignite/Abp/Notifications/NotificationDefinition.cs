using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Localization;

namespace Dignite.Abp.Notifications;

/// <summary>
/// A notification type registered by a business module: its stable name, display text, optional
/// permission/feature gating, and free-form attributes.
/// </summary>
public class NotificationDefinition
{
    public string Name { get; }

    public ILocalizableString DisplayName { get; set; }

    public ILocalizableString? Description { get; set; }

    /// <summary>
    /// When set, a user must be granted this permission both to subscribe and to receive the notification.
    /// </summary>
    public string? PermissionName { get; private set; }

    /// <summary>
    /// When set, this feature must be enabled in the notification tenant both to subscribe and to receive it.
    /// </summary>
    public string? FeatureName { get; private set; }

    /// <summary>Free-form extension bag — e.g. explicit external channel routing.</summary>
    public IDictionary<string, object?> Attributes { get; }

    public NotificationDefinition(string name, ILocalizableString displayName)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
        DisplayName = Check.NotNull(displayName, nameof(displayName));
        Attributes = new Dictionary<string, object?>();
    }

    public NotificationDefinition RequirePermission(string permissionName)
    {
        PermissionName = permissionName;
        return this;
    }

    public NotificationDefinition RequireFeature(string featureName)
    {
        FeatureName = featureName;
        return this;
    }

    public NotificationDefinition WithDescription(ILocalizableString description)
    {
        Description = description;
        return this;
    }

    public NotificationDefinition WithAttribute(string key, object? value)
    {
        Attributes[key] = value;
        return this;
    }

    /// <summary>Routes delivery to specific external notifier channels (by name).</summary>
    public NotificationDefinition UseChannels(params string[] channels)
    {
        if (channels == null || channels.Length == 0)
        {
            throw new ArgumentException(
                "At least one notification channel must be specified. Omit UseChannels(...) for inbox-only notifications.",
                nameof(channels));
        }

        if (channels.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Notification channel names cannot be null, empty or whitespace.", nameof(channels));
        }

        Attributes[NotificationChannels.AttributeName] = channels.Select(channel => channel.Trim()).ToArray();
        return this;
    }

    public string[]? GetChannelsOrNull()
    {
        return Attributes.TryGetValue(NotificationChannels.AttributeName, out var value) ? value as string[] : null;
    }
}
