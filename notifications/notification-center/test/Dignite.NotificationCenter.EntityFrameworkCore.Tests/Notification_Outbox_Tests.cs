using System;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;

namespace Dignite.NotificationCenter;

/// <summary>Runs the transactional event-box contract against the EF Core provider.</summary>
public class Notification_Outbox_Tests :
    Notification_Outbox_Tests<NotificationCenterEntityFrameworkCoreTestModule>
{
    protected override async Task<long> GetOutboxCountAsync()
    {
        return await GetRequiredService<IRepository<OutgoingEventRecord, Guid>>().GetCountAsync();
    }
}
