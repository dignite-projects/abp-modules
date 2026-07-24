using Dignite.NotificationCenter.MongoDB;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace Dignite.NotificationCenter;

[DependsOn(
    typeof(NotificationCenterTestBaseModule),
    typeof(NotificationCenterMongoDbModule)
    )]
public class NotificationCenterMongoDbTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Point ABP at this run's ephemeral MongoDB database.
        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = MongoDbFixture.GetRandomConnectionString();
        });

        // MongoDB transactions require a replica set and add no value to these single-document
        // store/app-service tests, so disable them (matches ABP's own MongoDB test modules).
        Configure<AbpUnitOfWorkDefaultOptions>(options =>
        {
            options.TransactionBehavior = UnitOfWorkTransactionBehavior.Disabled;
        });
    }
}
