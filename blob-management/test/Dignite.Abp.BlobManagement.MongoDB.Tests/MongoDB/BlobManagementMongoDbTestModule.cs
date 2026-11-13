using System;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.MongoDB;
using Volo.Abp.Uow;

namespace Dignite.Abp.BlobManagement.MongoDB;

[DependsOn(
    typeof(BlobManagementApplicationTestModule),
    typeof(BlobManagementMongoDbModule),
    typeof(AbpPermissionManagementMongoDbModule)
)]
public class BlobManagementMongoDbTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = MongoDbFixture.GetRandomConnectionString();
        });

        // As the repository's other MongoDB test modules (and ABP's own) do: a transaction on a fresh database fails as
        // soon as it creates a collection, which every first write through an application service does.
        Configure<AbpUnitOfWorkDefaultOptions>(options =>
        {
            options.TransactionBehavior = UnitOfWorkTransactionBehavior.Disabled;
        });
    }
}
