using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Data;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.MongoDB;

/// <summary>
/// The MongoDB definition store on the deployment's database. <see cref="DefinitionStoreTestApplication"/> adds it to
/// every application of a <see cref="MongoDbDefinitionStoreInfrastructure"/> deployment. Only the
/// <see cref="NotificationDefinitionStoreDbProperties.ConnectionStringName"/> connection string is set — no default one —
/// so the context must find its database under that name.
/// </summary>
[DependsOn(typeof(AbpNotificationsMongoDbModule))]
public class AbpNotificationsMongoDbTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var shared = (MongoDbDefinitionStoreInfrastructure)context.Services
            .GetSingletonInstance<SharedDefinitionStoreInfrastructure>();

        Configure<AbpDbConnectionOptions>(options =>
        {
            options.ConnectionStrings[NotificationDefinitionStoreDbProperties.ConnectionStringName] =
                shared.ConnectionString;
        });
    }
}
