using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.DistributedEvents;

namespace Dignite.NotificationCenter.EntityFrameworkCore;

[ConnectionStringName(NotificationCenterDbProperties.ConnectionStringName)]
public class NotificationCenterDbContext :
    AbpDbContext<NotificationCenterDbContext>,
    INotificationCenterDbContext,
    IHasEventInbox,
    IHasEventOutbox
{
    public DbSet<Notification> Notifications { get; set; } = default!;

    public DbSet<UserNotification> UserNotifications { get; set; } = default!;

    public DbSet<NotificationSubscription> NotificationSubscriptions { get; set; } = default!;

    // Transactional inbox/outbox support makes persisting the notification/inbox rows and publishing
    // NotificationDeliveryRequestedEto atomic. Channel delivery itself is best-effort and keeps no per-recipient state.
    public DbSet<IncomingEventRecord> IncomingEvents { get; set; } = default!;

    public DbSet<OutgoingEventRecord> OutgoingEvents { get; set; } = default!;

    public NotificationCenterDbContext(DbContextOptions<NotificationCenterDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureEventInbox();
        builder.ConfigureEventOutbox();
        builder.ConfigureNotificationCenter();
    }
}
