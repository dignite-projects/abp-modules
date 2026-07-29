using Volo.Abp.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Dignite.Abp.FlexFields.Demo.Data;

public class DemoDbSchemaMigrator : ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public DemoDbSchemaMigrator(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        
        /* We intentionally resolving the DemoDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<DemoDbContext>()
            .Database
            .MigrateAsync();

    }
}
