using System.Collections.Generic;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>
/// The definition store's switches, after ABP's <c>PermissionManagementOptions</c> (the two booleans) and
/// <c>AbpPermissionOptions</c> (the two deleted lists). Both booleans are turned off in a data migration environment.
/// </summary>
public class NotificationDefinitionStoreOptions
{
    /// <summary>
    /// Save this process's static definitions to the store at startup (in the background, with retries).
    /// Default: true.
    /// </summary>
    public bool SaveStaticNotificationsToDatabase { get; set; } = true;

    /// <summary>
    /// Read the definitions other processes saved, through <see cref="IDynamicNotificationDefinitionStore"/>. Turn it on
    /// in the process that serves the inbox for notifications it does not define — a dedicated notification service.
    /// Default: false.
    /// </summary>
    public bool IsDynamicNotificationStoreEnabled { get; set; }

    /// <summary>
    /// Names of definitions to delete from the store when this process saves. Several processes write to the same
    /// table, so a definition is never deleted just because this process no longer defines it; list it here instead.
    /// </summary>
    public HashSet<string> DeletedNotifications { get; } = new();

    /// <summary>Names of groups to delete from the store, with all of their definitions, when this process saves.</summary>
    public HashSet<string> DeletedNotificationGroups { get; } = new();
}
