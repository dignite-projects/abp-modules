using Dignite.NotificationCenter.MongoDB;
using Volo.Abp.Data;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace Dignite.NotificationCenter;

[DependsOn(
    typeof(NotificationCenterTestBaseModule),
    typeof(NotificationCenterMongoDbModule)
    )]
public class NotificationCenterMongoDbOutboxTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = MongoDbFixture.GetRandomConnectionString();
        });

        Configure<AbpUnitOfWorkDefaultOptions>(options =>
        {
            options.TransactionBehavior = UnitOfWorkTransactionBehavior.Enabled;
        });

        Configure<AbpDistributedEventBusOptions>(options =>
        {
            options.UseNotificationCenterMongoDbOutbox();
            options.Outboxes.Configure(config => config.IsSendingEnabled = false);
            options.Inboxes.Configure(config => config.IsProcessingEnabled = false);
        });
    }
}
