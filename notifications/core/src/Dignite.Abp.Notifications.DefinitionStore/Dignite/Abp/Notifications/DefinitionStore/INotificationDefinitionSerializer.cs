using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>Turns definitions into records, as ABP's <c>IPermissionDefinitionSerializer</c>.</summary>
public interface INotificationDefinitionSerializer
{
    Task<(NotificationGroupDefinitionRecord[], NotificationDefinitionRecord[])> SerializeAsync(
        IEnumerable<NotificationGroupDefinition> notificationGroups);

    Task<NotificationGroupDefinitionRecord> SerializeAsync(NotificationGroupDefinition notificationGroup);

    Task<NotificationDefinitionRecord> SerializeAsync(NotificationDefinition notification);
}
