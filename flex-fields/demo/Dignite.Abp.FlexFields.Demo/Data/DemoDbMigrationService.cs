using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace Dignite.Abp.FlexFields.Demo.Data;

public class DemoDbMigrationService : ITransientDependency
{
    public ILogger<DemoDbMigrationService> Logger { get; set; }

    private readonly IDataSeeder _dataSeeder;
    private readonly DemoDbSchemaMigrator _dbSchemaMigrator;
    private readonly ICurrentTenant _currentTenant;

    public DemoDbMigrationService(
        IDataSeeder dataSeeder,
        DemoDbSchemaMigrator dbSchemaMigrator,
        ICurrentTenant currentTenant)
    {
        _dataSeeder = dataSeeder;
        _dbSchemaMigrator = dbSchemaMigrator;
        _currentTenant = currentTenant;

        Logger = NullLogger<DemoDbMigrationService>.Instance;
    }

    public async Task MigrateAsync()
    {
        Logger.LogInformation("Started database migrations...");

        await MigrateDatabaseSchemaAsync();
        await SeedDataAsync();

        Logger.LogInformation($"Successfully completed host database migrations.");
        Logger.LogInformation("You can safely end this process...");
    }

    private async Task MigrateDatabaseSchemaAsync()
    {
        await _dbSchemaMigrator.MigrateAsync();
    }

    private async Task SeedDataAsync()
    {
        await _dataSeeder.SeedAsync(new DataSeedContext()
            .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, DemoConsts.AdminEmailDefaultValue)
            .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, DemoConsts.AdminPasswordDefaultValue)
        );
    }
}
