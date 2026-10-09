using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Dignite.Abp.Notifications.DefinitionStore;

/// <summary>The definition records, as ABP's <c>IPermissionDefinitionRecordRepository</c>.</summary>
public interface INotificationDefinitionRecordRepository : IBasicRepository<NotificationDefinitionRecord, Guid>
{
    Task<NotificationDefinitionRecord?> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}
