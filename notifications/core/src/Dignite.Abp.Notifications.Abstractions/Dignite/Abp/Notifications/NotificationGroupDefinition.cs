using System.Collections.Generic;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Localization;

namespace Dignite.Abp.Notifications;

/// <summary>
/// A named category of notification definitions (e.g. "Order"), used to group a user's inbox and subscriptions.
/// Mirrors ABP's <c>PermissionGroupDefinition</c>: every definition belongs to exactly one group. A group is
/// definition-time metadata only — it is never persisted, so regrouping needs no data migration.
/// </summary>
public class NotificationGroupDefinition
{
    public string Name { get; }

    public ILocalizableString DisplayName
    {
        get => _displayName;
        set => _displayName = Check.NotNull(value, nameof(value));
    }
    private ILocalizableString _displayName = default!;

    /// <summary>The group's definitions, in registration order.</summary>
    public IReadOnlyList<NotificationDefinition> Notifications => _notifications.ToList();
    private readonly List<NotificationDefinition> _notifications;

    private readonly NotificationDefinitionContext _context;

    protected internal NotificationGroupDefinition(
        NotificationDefinitionContext context,
        string name,
        ILocalizableString? displayName = null)
    {
        _context = Check.NotNull(context, nameof(context));
        Name = Check.NotNullOrWhiteSpace(name, nameof(name));
        DisplayName = displayName ?? new FixedLocalizableString(name);
        _notifications = new List<NotificationDefinition>();
    }

    /// <summary>
    /// Adds a definition to this group. Definition names are unique across all groups (ordinal, case-sensitive).
    /// </summary>
    public virtual NotificationDefinition AddNotification(string name, ILocalizableString displayName)
    {
        var definition = new NotificationDefinition(Name, name, displayName);
        _context.AddDefinition(definition);
        _notifications.Add(definition);
        return definition;
    }

    public NotificationDefinition? GetNotificationOrNull(string name)
    {
        return _notifications.FirstOrDefault(definition => definition.Name == name);
    }

    public override string ToString()
    {
        return $"[{nameof(NotificationGroupDefinition)} {Name}]";
    }
}
