using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Dignite.FileExplorer.Web.Host.Data;

public class HostDbMigrationService : ITransientDependency
{
    public ILogger<HostDbMigrationService> Logger { get; set; }

    private readonly IDataSeeder _dataSeeder;
    private readonly HostDbSchemaMigrator _dbSchemaMigrator;
    private readonly ITenantRepository _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;

    public HostDbMigrationService(
        IDataSeeder dataSeeder,
        HostDbSchemaMigrator dbSchemaMigrator,
        ITenantRepository tenantRepository,
        ICurrentTenant currentTenant,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
    {
        _dataSeeder = dataSeeder;
        _dbSchemaMigrator = dbSchemaMigrator;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;

        Logger = NullLogger<HostDbMigrationService>.Instance;
    }

    public async Task MigrateAsync()
    {
        Logger.LogInformation("Started database migrations...");

        await MigrateDatabaseSchemaAsync();
        await SeedDataAsync();

        Logger.LogInformation($"Successfully completed host database migrations.");

        if (HostModule.IsMultiTenant)
        {
            var tenants = await _tenantRepository.GetListAsync(includeDetails: true);

            var migratedDatabaseSchemas = new HashSet<string>();
            foreach (var tenant in tenants)
            {
                using (_currentTenant.Change(tenant.Id))
                {
                    if (tenant.ConnectionStrings.Any())
                    {
                        var tenantConnectionStrings = tenant.ConnectionStrings
                            .Select(x => x.Value)
                            .ToList();

                        if (!migratedDatabaseSchemas.IsSupersetOf(tenantConnectionStrings))
                        {
                            await MigrateDatabaseSchemaAsync(tenant);

                            migratedDatabaseSchemas.AddIfNotContains(tenantConnectionStrings);
                        }
                    }

                    await SeedDataAsync(tenant);
                }

                Logger.LogInformation($"Successfully completed {tenant.Name} tenant database migrations.");
            }

            Logger.LogInformation("Successfully completed all database migrations.");
        }
        
        Logger.LogInformation("You can safely end this process...");
    }

    private async Task MigrateDatabaseSchemaAsync(Tenant? tenant = null)
    {
        Logger.LogInformation(
            $"Migrating schema for {(tenant == null ? "host" : tenant.Name + " tenant")} database...");
        
        await _dbSchemaMigrator.MigrateAsync();
    }

    private async Task SeedDataAsync(Tenant? tenant = null)
    {
        Logger.LogInformation($"Executing {(tenant == null ? "host" : tenant.Name + " tenant")} database seed...");
        
        await _dataSeeder.SeedAsync(new DataSeedContext(tenant?.Id)
            .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, HostConsts.AdminEmailDefaultValue)
            .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, GetAdminPassword())
        );
    }

    private string GetAdminPassword()
    {
        var configuredPassword = _configuration[HostConsts.AdminPasswordConfigurationKey];
        if (!string.IsNullOrWhiteSpace(configuredPassword))
        {
            return configuredPassword;
        }

        if (_hostEnvironment.IsDevelopment())
        {
            Logger.LogWarning(
                "Using the ABP development admin password. Set {ConfigurationKey} before sharing this environment.",
                HostConsts.AdminPasswordConfigurationKey);
            return IdentityDataSeedContributor.AdminPasswordDefaultValue;
        }

        throw new InvalidOperationException(
            $"{HostConsts.AdminPasswordConfigurationKey} must be configured outside the Development environment.");
    }

}
