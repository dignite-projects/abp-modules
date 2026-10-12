using System;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.Notifications.EntityFrameworkCore;

public class EfCoreNotificationGroupDefinitionRecordRepository :
    EfCoreRepository<INotificationDefinitionStoreDbContext, NotificationGroupDefinitionRecord, Guid>,
    INotificationGroupDefinitionRecordRepository
{
    public EfCoreNotificationGroupDefinitionRecordRepository(
        IDbContextProvider<INotificationDefinitionStoreDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }
}
