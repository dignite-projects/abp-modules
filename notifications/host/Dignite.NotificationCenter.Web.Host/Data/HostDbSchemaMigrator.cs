using Volo.Abp.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Dignite.NotificationCenter.Web.Host.Data;

public class HostDbSchemaMigrator : ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public HostDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        
        /* We intentionally resolving the HostDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<HostDbContext>()
            .Database
            .MigrateAsync();

    }
}
