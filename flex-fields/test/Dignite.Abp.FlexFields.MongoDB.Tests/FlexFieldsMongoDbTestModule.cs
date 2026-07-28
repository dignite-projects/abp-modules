using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MongoDB;
using Volo.Abp.Uow;

namespace Dignite.Abp.FlexFields.MongoDB;

[DependsOn(
    typeof(AbpTestBaseModule),
    typeof(AbpAutofacModule),
    typeof(FlexFieldsMongoDbModule)
    )]
public class FlexFieldsMongoDbTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Point ABP at this run's ephemeral MongoDB database.
        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings.Default = MongoDbFixture.GetRandomConnectionString();
        });

        // MongoDB transactions require a replica set and add no value to these tests, so disable them
        // (matches ABP's own MongoDB test modules).
        Configure<AbpUnitOfWorkDefaultOptions>(options =>
        {
            options.TransactionBehavior = UnitOfWorkTransactionBehavior.Disabled;
        });

        // The downstream owns its context and its collections - the kernel registers none of this.
        context.Services.AddMongoDbContext<TestFlexFieldsMongoDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
            options.AddRepository<TestField, TestFieldRepository>();
        });

        // ITransientDependency on the repository base is not enough for IFlexFieldRepository<TField> unless
        // the concrete class name ends with "FlexFieldRepository" - see MongoFlexFieldRepositoryBase.
        context.Services.AddTransient<IFlexFieldRepository<TestField>, TestFieldRepository>();

        // Exposed as itself only; IFlexFieldValueMigrator<TestArticle> stays bound to the kernel's
        // open-generic registration, which is what the migrator tests here resolve.
        context.Services.AddTransient<SmallPageFlexFieldValueMigrator>();
    }
}
