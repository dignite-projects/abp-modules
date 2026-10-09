using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;

namespace Dignite.Abp.Notifications.EntityFrameworkCore;

/// <summary>
/// The EF Core definition store on the deployment's SQLite connection. <see cref="DefinitionStoreTestApplication"/>
/// adds it to every application of a <see cref="SqliteDefinitionStoreInfrastructure"/> deployment.
/// </summary>
[DependsOn(
    typeof(AbpNotificationsEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
    )]
public class AbpNotificationsEntityFrameworkCoreTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var shared = (SqliteDefinitionStoreInfrastructure)context.Services
            .GetSingletonInstance<SharedDefinitionStoreInfrastructure>();

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(dbContext => dbContext.DbContextOptions.UseSqlite(shared.Connection));
        });
    }
}
