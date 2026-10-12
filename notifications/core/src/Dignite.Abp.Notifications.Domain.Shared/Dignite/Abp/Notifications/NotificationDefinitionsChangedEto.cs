using System;
using System.Collections.Generic;
using Volo.Abp.EventBus;

namespace Dignite.Abp.Notifications;

/// <summary>
/// Published by <c>StaticNotificationDefinitionSaver</c> when a save inserted or changed notification definitions in
/// the store, with their names — ABP's <c>DynamicPermissionDefinitionsChangedEto</c>, published the same way by
/// <c>StaticPermissionSaver</c>. It goes through the distributed event bus in the saver's unit of work, so with an outbox
/// it is written in the same transaction as the records.
/// </summary>
/// <remarks>
/// It is a notification for other services, not how the dynamic store learns about changes: every
/// <c>DynamicNotificationDefinitionStore</c> still compares the common stamp in the distributed cache, and nothing in
/// this module handles the event. As in ABP, a save that only changes or deletes groups, or only deletes definitions,
/// publishes nothing.
/// </remarks>
[Serializable]
[EventName("Dignite.Abp.Notifications.NotificationDefinitionsChanged")]
public class NotificationDefinitionsChangedEto
{
    /// <summary>The names of the notification definitions the save inserted or changed.</summary>
    public List<string> Notifications { get; set; } = new();
}
