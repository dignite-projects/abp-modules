using System;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Abp.Notifications;

/// <summary>The group records, as ABP's <c>IPermissionGroupDefinitionRecordRepository</c>.</summary>
public interface INotificationGroupDefinitionRecordRepository : IBasicRepository<NotificationGroupDefinitionRecord, Guid>
{
}
