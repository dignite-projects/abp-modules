using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Dignite.Abp.Notifications.EntityFrameworkCore;

public class EfCoreNotificationDefinitionRecordRepository :
    EfCoreRepository<INotificationDefinitionStoreDbContext, NotificationDefinitionRecord, Guid>,
    INotificationDefinitionRecordRepository
{
    public EfCoreNotificationDefinitionRecordRepository(
        IDbContextProvider<INotificationDefinitionStoreDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<NotificationDefinitionRecord?> FindByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        return await (await GetDbSetAsync())
            .OrderBy(x => x.Id)
            .FirstOrDefaultAsync(record => record.Name == name, GetCancellationToken(cancellationToken));
    }
}
