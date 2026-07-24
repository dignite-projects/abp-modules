using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Provides the startup-validated notification definitions. Name registration and lookup use ordinal,
/// case-sensitive comparison. This interface is intentionally replaceable: consuming hosts may supply a custom
/// definition registry/availability implementation, and startup resolves the registered replacement before it
/// initializes definitions.
/// </summary>
public interface INotificationDefinitionManager
{
    /// <summary>Gets a definition by its ordinal, case-sensitive name.</summary>
    NotificationDefinition Get(string name);

    /// <summary>Gets a definition by its ordinal, case-sensitive name, or <see langword="null"/>.</summary>
    NotificationDefinition? GetOrNull(string name);

    IReadOnlyList<NotificationDefinition> GetAll();

    /// <summary>
    /// Evaluates whether the user may subscribe to and receive the notification in the ambient tenant/host context.
    /// </summary>
    Task<bool> IsAvailableAsync(string name, Guid userId);

    Task<IReadOnlyList<NotificationDefinition>> GetAllAvailableAsync(Guid userId);
}
