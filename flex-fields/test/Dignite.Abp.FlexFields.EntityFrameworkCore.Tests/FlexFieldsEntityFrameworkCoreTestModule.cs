using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;

namespace Dignite.Abp.FlexFields.EntityFrameworkCore;

[DependsOn(
    typeof(AbpTestBaseModule),
    typeof(AbpAutofacModule),
    typeof(FlexFieldsEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
    )]
public class FlexFieldsEntityFrameworkCoreTestModule : AbpModule
{
    private SqliteConnection? _sqliteConnection;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ConfigureInMemorySqlite(context.Services);

        // The downstream's job, not the kernel's - the kernel ships no DbContext to register.
        context.Services.AddAbpDbContext<TestFlexFieldsDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
            options.AddRepository<TestField, TestFieldRepository>();
        });

        // ABP's conventional registration only auto-exposes a custom repository interface when the
        // concrete class name ends with the interface name (minus "I") - e.g. EfCoreIdentityUserRepository
        // matching IIdentityUserRepository. TestFieldRepository doesn't end in "FlexFieldRepository", so
        // IFlexFieldRepository<TestField> needs this explicit line; a real downstream hits the same thing
        // unless its own class name happens to match.
        context.Services.AddTransient<IFlexFieldRepository<TestField>, TestFieldRepository>();
    }

    private void ConfigureInMemorySqlite(IServiceCollection services)
    {
        _sqliteConnection = CreateDatabaseAndGetConnection();

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(context =>
            {
                context.DbContextOptions.UseSqlite(_sqliteConnection!);
            });
        });
    }

    private static SqliteConnection CreateDatabaseAndGetConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TestFlexFieldsDbContext>().UseSqlite(connection).Options;
        using (var dbContext = new TestFlexFieldsDbContext(options))
        {
            // Proves the three kernel EntityTypeBuilder extensions produce a buildable model + real schema.
            dbContext.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        return connection;
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        _sqliteConnection?.Dispose();
    }
}
